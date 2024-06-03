#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEditor;
using UnityEngine;

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

    /// <summary>
    /// Represents a single, instantiated pipeline for building and maintaining all proxy objects.
    /// </summary>
    internal class ProxyPipeline
    {
        private List<(IRenderFilter, ImmutableList<Renderer>)> _filterGroups;
        private Dictionary<Renderer, List<IRenderFilter>> _rendererToFilters;
        private ImmutableDictionary<Renderer, ProxyNode> _meshLeaves;

        private TaskCompletionSource<object> _invalidater = new();
        public Task InvalidatedTask => _invalidater.Task;
        public bool Invalidated => InvalidatedTask.IsCompleted;

        private Task BuildPipelineTask;

        private bool _disposeCalled;
        private Task _disposeTask;

        public bool BuildCompleted => BuildPipelineTask.IsCompleted;
        public bool Aborted => BuildPipelineTask.IsCompleted && _meshLeaves == null;

        public IImmutableSet<Renderer> Renderers => _meshLeaves.Keys.ToImmutableHashSet();

        private IImmutableList<ProxyNodeKey> Nodes = ImmutableList<ProxyNodeKey>.Empty;

        public ProxyPipeline(NodeGraph graph, IEnumerable<IRenderFilter> filters)
        {
            using (new SyncContextScope(ReactiveQueryScheduler.SynchronizationContext))
            {
                BuildPipelineTask = Build(graph, filters).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Debug.LogException(t.Exception);
                    }

                    EditorApplication.delayCall += SceneView.RepaintAll;
                });
            }
        }

        public void Invalidate()
        {
            _invalidater.TrySetResult(null);
        }

        private async Task Build(NodeGraph graph, IEnumerable<IRenderFilter> filters)
        {
            var ctx = new ComputeContext(() => "Preview pipeline: Construct pipeline");

            ctx.Invalidate = () => _invalidater.TrySetResult(null);
            ctx.OnInvalidate = InvalidatedTask;

            filters = filters.ToList();

            _filterGroups = await CollectInterestingRenderers(filters);

            _rendererToFilters = new Dictionary<Renderer, List<IRenderFilter>>();
            foreach (var group in _filterGroups)
            {
                foreach (var renderer in group.Item2)
                {
                    if (!_rendererToFilters.TryGetValue(renderer, out var list))
                    {
                        list = new List<IRenderFilter>();
                        _rendererToFilters.Add(renderer, list);
                    }

                    list.Add(group.Item1);
                }
            }

            var allRenderers = _filterGroups.SelectMany(p => p.Item2).ToHashSet();
            var nodes = ImmutableList<ProxyNodeKey>.Empty.ToBuilder();

            var leaves = ImmutableDictionary<Renderer, ProxyNode>.Empty;
            foreach (var renderer in allRenderers)
            {
                var node = graph.GetOrCreate(new ProxyNodeKey(renderer), () => new ProxyNode(renderer));
                leaves = leaves.Add(renderer, node);
                nodes.Add(node.Key);
                _ = node.InvalidatedTask.ContinueWith(_ => Invalidate());
            }

            if (Invalidated)
            {
                // Abort ASAP
                return;
            }

            foreach (var pair in _filterGroups)
            {
                var (filter, sourceRenderers) = pair;
                var sources = sourceRenderers.Select(r => leaves[r].Id);
                var key = new ProxyNodeKey(filter, sources);

                var node = graph.GetOrCreate(key, () => new ProxyNode(filter, sourceRenderers, leaves));
                nodes.Add(node.Key);
                _ = node.InvalidatedTask.ContinueWith(_ => Invalidate());

                foreach (var source in sourceRenderers)
                {
                    leaves = leaves.SetItem(source, node);
                }
            }

            _meshLeaves = leaves;
            Nodes = nodes.ToImmutable();
        }

        private async Task<List<(IRenderFilter, ImmutableList<Renderer>)>> CollectInterestingRenderers(
            IEnumerable<IRenderFilter> filters)
        {
            var ctx = new ComputeContext(() => "Preview pipeline: Collect interesting renderers");

            ctx.Invalidate = () => _invalidater.TrySetResult(null);
            ctx.OnInvalidate = InvalidatedTask;

            var result = new List<(IRenderFilter, ImmutableList<Renderer>)>();
            foreach (var filter in filters)
            {
                var groups = await ctx.Observe(filter.TargetGroups);
                if (groups.Count == 0) continue;

                // TODO: Validate groups are non-overlapping

                foreach (var group in groups)
                {
                    if (group.Count == 0) continue;
                    result.Add((filter, group.ToImmutableList()));
                }
            }

            return result;
        }


        public MeshState GetState(Renderer originalRenderer)
        {
            if (_disposeCalled) throw new ObjectDisposedException("ProxyPipeline");
            if (!BuildCompleted) throw new InvalidOperationException("Pipeline not ready");

            if (_meshLeaves.TryGetValue(originalRenderer, out var node))
            {
                if (node.PrepareTask.IsCompleted &&
                    node.PrepareTask.Result?.TryGetValue(originalRenderer, out var state) == true)
                {
                    return state;
                }
            }

            return null;
        }

        public void RunOnFrame(Renderer original, Renderer replacement)
        {
            var filters = _rendererToFilters[original];

            foreach (var filter in filters)
            {
                filter.OnFrame(original, replacement);
            }
        }

        public void CollectNodes(HashSet<ProxyNodeKey> toRetain)
        {
            foreach (var node in Nodes)
            {
                toRetain.Add(node);
            }
        }
    }
}