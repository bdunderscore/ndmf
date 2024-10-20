#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview.trace;
using UnityEngine;
using UnityEngine.Profiling;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal enum PipelineStatus
    {
        NotReady,
        Ready,
        Invalidated,
        Disposed
    }

    class StageDescriptor
    {
        #region Initial configuration

        public readonly IRenderFilter Filter;
        public readonly ImmutableList<RenderGroup> Originals;

        public StageDescriptor(TargetSet.Stage targetStage)
        {
            Filter = targetStage.Filter;
            Originals = targetStage.Groups;
        }
        
        public StageDescriptor(StageDescriptor prior)
        {
            Filter = prior.Filter;
            Originals = prior.Originals;
        }

        #endregion

        public List<Task<NodeController>> NodeTasks = new();
    } 

    /// <summary>
    /// Represents a single, instantiated pipeline for building and maintaining all proxy objects.
    /// </summary>
    internal class ProxyPipeline
    {
        private TargetSet _targetSet;
        private List<StageDescriptor> _stages = new();
        private Dictionary<Renderer, ProxyObjectController> _proxies = new();
        private List<NodeController> _nodes = new(); // in OnFrame execution order

        private Task _buildTask;

        private TaskCompletionSource<object> _completedBuild = new();

        internal ImmutableDictionary<Renderer, Renderer> OriginalToProxyRenderer =
            ImmutableDictionary<Renderer, Renderer>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> OriginalToProxyObject =
            ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> ProxyToOriginalObject =
            ImmutableDictionary<GameObject, GameObject>.Empty;

        private readonly long _generation;
        
        // ReSharper disable once NotAccessedField.Local
        // needed to prevent GC of the ComputeContext
        private ComputeContext _ctx;
        internal bool IsInvalidated => _ctx.OnInvalidate.IsCompleted;
        
        internal void Invalidate()
        {
            _ctx.Invalidate();
        }

        private readonly Action InvalidateAction;

        public bool IsReady => _buildTask.IsCompletedSuccessfully;
        public bool IsFailed => _buildTask.IsFaulted;

        public IEnumerable<(Renderer, Renderer)> Renderers
            => _proxies.Select(kvp => (kvp.Key, kvp.Value.Renderer));

        public ProxyPipeline(ProxyObjectCache proxyCache, IEnumerable<IRenderFilter> filters,
            ProxyPipeline priorPipeline = null)
        {
            _generation = (priorPipeline?._generation ?? 0) + 1;
            InvalidateAction = Invalidate;

            var buildEvent = TraceBuffer.RecordTraceEvent(
                "ProxyPipeline.Build",
                (ev) => $"Pipeline {((ProxyPipeline)ev.Arg0)._generation}: Start build",
                arg0: this
            );

            using (var scope = NDMFSyncContext.Scope())
            using (var evScope = buildEvent.Scope())
            {
                _buildTask = Task.Factory.StartNew(
                    _ => Build(proxyCache, filters, priorPipeline),
                    null,
                    CancellationToken.None,
                    0,
                    TaskScheduler.FromCurrentSynchronizationContext()
                ).Unwrap();
            }
        }

        private static void OnInvalidateRedraw(ProxyPipeline obj)
        {
            RepaintTrigger.RequestRepaint();
        }

        private async Task Build(ProxyObjectCache proxyCache, IEnumerable<IRenderFilter> filters,
            ProxyPipeline priorPipeline)
        {
            await TaskThrottle.MaybeThrottle();
            
            Profiler.BeginSample("ProxyPipeline.Build.Synchronous");
            var context = new ComputeContext($"ProxyPipeline {_generation}");
            _ctx = context; // prevent GC
            
            _ctx.InvokeOnInvalidate(this, OnInvalidateRedraw);

#if NDMF_DEBUG
            Debug.Log($"Building pipeline {_generation}");
#endif

            var filterList = filters.ToImmutableList();
            _targetSet = priorPipeline?._targetSet?.Refresh(filterList) ?? new TargetSet(filterList);

            var activeStages = _targetSet.ResolveActiveStages(context);

#if NDMF_TRACE_FILTERS
            var sb = new System.Text.StringBuilder();
            foreach (var stage in activeStages)
            {
                sb.Append("\t").Append(stage.Filter).Append(" on ").Append(stage.Groups).Append("\n");
            }
            UnityEngine.Debug.Log($"[ProxyPipeline] Active stages for pipeline {_generation}:\n{sb}");
#endif

            Dictionary<Renderer, Task<NodeController>> nodeTasks = new();
            int total_nodes = 0;
            int reused = 0;
            int refresh_failed = 0;

            for (int i = 0; i < activeStages.Count(); i++)
            {
                var stageTemplate = activeStages[i];
                
                Profiler.BeginSample("new StageDescriptor");
                StageDescriptor stage = new StageDescriptor(stageTemplate);

                Profiler.EndSample();
                
                _stages.Add(stage);

                var prior = priorPipeline?._stages.ElementAtOrDefault(i);
                if (prior?.Filter != stage.Filter)
                {
                    prior = null;
                }

                int groupIndex = -1;
                foreach (var group_raw in stage.Originals.OrderBy(g => g.GetHashCode()))
                {
                    var group = group_raw.FilterLive();
                    total_nodes++;
                    
                    groupIndex++;

                    var trace = $"Gen{_generation}/Stage{i}/Group{groupIndex}";
                    
                    var resolved = group.Renderers.Select(r =>
                    {
                        if (r == null)
                        {
                            Debug.Log("Renderer deleted during proxy pipeline construction: " +
                                      group.DebugNames[r]);
                            Invalidate();
                            return null;
                        }

                        if (nodeTasks.TryGetValue(r, out var task))
                        {
                            return task.ContinueWith(task1 =>
                                (r, task1.Result.GetProxyFor(r), task1.Result.ObjectRegistry));
                        }
                        else
                        {
                            ProxyObjectController priorProxy = null;
                            priorPipeline?._proxies.TryGetValue(r, out priorProxy);
                            
                            var proxy = new ProxyObjectController(proxyCache, r, priorProxy);
                            proxy.InvalidateMonitor.Invalidates(context);

                            if (!proxy.OnPreFrame()) Invalidate();
                            // OnPreFrame can enable rendering, turn it off for now (until the pipeline goes active and
                            // we render for real).
                            _proxies.Add(r, proxy);

                            var registry = new ObjectRegistry(null);
                            ((IObjectRegistry)registry).RegisterReplacedObject(r, proxy.Renderer);

                            return Task.FromResult((r, proxy, registry));
                        }
                    }).Where(r => r != null).ToList();

                    if (resolved.Count == 0)
                    {
                        continue;
                    }

                    var priorNode = prior?.NodeTasks.ElementAtOrDefault(groupIndex);
                    var sameGroup = Equals(priorNode?.Result.Group, group);
                    if (priorNode?.IsCompletedSuccessfully != true || !sameGroup)
                    {
                        //System.Diagnostics.UnityEngine.Debug.Log("Failed to reuse node: priorNode != null: " + (priorNode != null) + ", sameGroup: " + sameGroup);
                        priorNode = null;
                    }

                    var node = Task.WhenAll(resolved).ContinueWith(async items =>
                        {
                            await TaskThrottle.MaybeThrottle();
                            
                            var proxies = items.Result.ToList();

#if NDMF_DEBUG
                            Debug.Log(
                                $"Creating node for {stage.Filter} on {group.Renderers[0].gameObject.name} for generation {_generation}");
#endif
                            NodeController node = null;
                            
                            if (priorNode != null)
                            {
                                RenderAspects changeFlags = proxies.Select(p => p.Item2.ChangeFlags)
                                    .Aggregate((a, b) => a | b);

                                node = await priorNode.Result.Refresh(proxies, changeFlags, trace);
                                if (node != null)
                                {
                                    reused++;

                                }
                                else
                                {
                                    refresh_failed++;
                                }
                            }

                            if (node == null)
                            {
                                node = await NodeController.Create(stage.Filter, group, items.Result.ToList(), trace);
                                // Force a rebuild of downstream nodes
                                node.WhatChanged = RenderAspects.Everything;
                            }
                            
                            foreach (var proxy in proxies)
                            {
                                proxy.Item2.ChangeFlags |= node.WhatChanged;
                            }

                            return node;
                        })
                        .Unwrap();

                    stage.NodeTasks.Add(node);

                    foreach (var renderer in group.Renderers)
                    {
                        nodeTasks[renderer] = node;
                    }
                }
            }
            
            Profiler.EndSample();

            await Task.WhenAll(_stages.SelectMany(s => s.NodeTasks))
                .ContinueWith(result =>
                {
                    TraceBuffer.RecordTraceEvent(
                        "ProxyPipeline.Build",
                        (ev) => $"Pipeline {((ProxyPipeline)ev.Arg0)._generation}: Build complete",
                        arg0: this
                    );
                    _completedBuild.TrySetResult(null);
                    RepaintTrigger.RequestRepaint();
                });

            //UnityEngine.Debug.Log($"Total nodes: {total_nodes}, reused: {reused}, refresh failed: {refresh_failed}");

            foreach (var (r, proxy) in _proxies)
            {
                proxy.FinishSetup();

                if (proxy.Renderer == null)
                {
                    Invalidate();
                    continue;
                }

                OriginalToProxyRenderer = OriginalToProxyRenderer.Add(r, proxy.Renderer);
                OriginalToProxyObject = OriginalToProxyObject.Add(r.gameObject, proxy.Renderer.gameObject);
                ProxyToOriginalObject = ProxyToOriginalObject.Add(proxy.Renderer.gameObject, r.gameObject);
            }
            
#if NDMF_DEBUG
            Debug.Log($"Pipeline {_generation} is ready");
#endif

            foreach (var stage in _stages)
            {
                foreach (var node in stage.NodeTasks)
                {
                    var resolvedNode = await node;
                    _nodes.Add(resolvedNode);
                    _ = resolvedNode.OnInvalidate.ContinueWith(_ => Invalidate());
                }
            }
        }

        public void OnFrame(bool isSceneView)
        {
            if (!IsReady) return;

            using (var scope = FrameTimeLimiter.OpenFrameScope())
            {
                if (!scope.ShouldContinue()) return;

                foreach (var pair in _proxies)
                {
                    if (!pair.Value.OnPreFrame())
                    {
                        Invalidate();
                    }
                    
                    if (!scope.ShouldContinue())
                    {
                        RepaintTrigger.RequestRepaint();
                        return;
                    }
                }

                foreach (var node in _nodes)
                {
                    if (!scope.ShouldContinue())
                    {
                        RepaintTrigger.RequestRepaint();
                        return;
                    }

                    node.OnFrame();
                }

                foreach (var pair in _proxies)
                {
                    if (!scope.ShouldContinue())
                    {
                        RepaintTrigger.RequestRepaint();
                        return;
                    }

                    pair.Value.FinishPreFrame(isSceneView);
                }
            }
        }

        public void Dispose()
        {
            // We need to make sure this task runs on the unity main thread so it can delete the proxy objects
            _completedBuild.Task.ContinueWith(_ =>
                {
                    foreach (var stage in _stages)
                    {
                        foreach (var node in stage.NodeTasks)
                        {
                            if (node.IsCompletedSuccessfully)
                            {
                                node.Result.Dispose();
                            }
                        }
                    }

                    foreach (var proxy in _proxies.Values)
                    {
                        proxy.Dispose();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.RunContinuationsAsynchronously,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        public void ShowError()
        {
            Debug.LogException(_buildTask.Exception);
        }
    }
}