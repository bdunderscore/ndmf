#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview.trace;
using UnityEngine;
using UnityEngine.Profiling;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class NodeController : IDisposable
    {
        private class RefCount
        {
            public int Count = 1;
        }

        private readonly RenderGroup _group;
        private readonly IRenderFilter _filter;
        private readonly IRenderFilterNode _node;
        private readonly List<(Renderer, ProxyObjectController)> _proxies;
        private readonly RefCount _refCount;

        private readonly ComputeContext _context;

        private CustomSampler _profileSampler_onFrame;
        
        internal RenderAspects WhatChanged = RenderAspects.Everything;
        internal RenderGroup Group => _group;
        internal Task OnInvalidate => _context.OnInvalidate;
        internal bool IsInvalidated => OnInvalidate.IsCompleted;

        internal ObjectRegistry ObjectRegistry { get; private set; }

        internal ProxyObjectController GetProxyFor(Renderer r)
        {
            return _proxies.Find(p => p.Item1 == r).Item2;
        }

        private NodeController(
            IRenderFilter filter,
            RenderGroup group,
            IRenderFilterNode node,
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies,
            RefCount refCount,
            ComputeContext context,
            ObjectRegistry registry
        )
        {
            _filter = filter;
            _group = group;
            _node = node;
            _proxies = proxies.Select(tuple => (tuple.Item1, tuple.Item2)).ToList();
            _refCount = refCount;
            _context = context;
            ObjectRegistry = registry;
            
            _profileSampler_onFrame = CustomSampler.Create(filter.GetType() + ".OnFrame");
            
            OnFrame();
        }

        internal void OnFrame()
        {
            _profileSampler_onFrame.Begin();
            _node.OnFrameGroup();
            _profileSampler_onFrame.End();
            
            foreach (var (original, proxy) in _proxies)
            {
                if (original != null && proxy.Renderer != null)
                {
                    _profileSampler_onFrame.Begin(original.gameObject);
                    try
                    {
                        _node.OnFrame(original, proxy.Renderer);
                    }
                    finally
                    {
                        _profileSampler_onFrame.End();
                    }
                }
            }
        }

        public static Task<NodeController> Create(
            IRenderFilter filter,
            RenderGroup group,
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies,
            string trace
        )
        {
            return Create(filter, group, ObjectRegistry.Merge(null, proxies.Select(p => p.Item3)), proxies, trace);
        }

        private static async Task<NodeController> Create(
            IRenderFilter filter,
            RenderGroup group,
            ObjectRegistry registry,
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies,
            string trace
        )
        {
            var ev = TraceBuffer.RecordTraceEvent(
                "NodeController.Create", 
                (ev) => $"NodeController: Create for {ev.Arg0} on {ev.Arg1}",
                arg0: filter,
                arg1: group
            );

            using (ev.Scope())
            {

                AsyncProfiler.PushProfilerContext("NodeController.Create[" + filter + "]",
                    group.Renderers[0].gameObject);
                var context =
                    new ComputeContext("NodeController " + trace + " for " + filter + " on " +
                                       group.Renderers[0].gameObject.name);
#if NDMF_TRACE_FILTERS
            UnityEngine.Debug.Log("[NodeController Create] " + trace + " Filter=" + filter + " Group=" +
                      group.Renderers[0].gameObject.name +
                      " Registry dump:\n" + registry.RegistryDump());
#endif
                IRenderFilterNode node;
                using (var scope = new ObjectRegistryScope(registry))
                {
                    var savedMaterials = group.Renderers.Select(r => r.sharedMaterials).ToArray();

                    node = await filter.Instantiate(
                        group,
                        proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                        context
                    );

                    for (var i = 0; i < group.Renderers.Count; i++)
                    {
                        if (group.Renderers[i].sharedMaterials.SequenceEqual(savedMaterials[i])) continue;

                        Debug.LogWarning("[NodeController Create] Renderer " + group.Renderers[i].gameObject.name +
                                         " sharedMaterials changed during instantiation of " + filter + " in " +
                                         " group " + group + ". Restoring original materials.");
                        group.Renderers[i].sharedMaterials = savedMaterials[i];
                    }
                }

#if NDMF_TRACE_FILTERS
            Debug.Log("[NodeController Post-Create] " + trace + " Filter=" + filter + " Group=" +
                      group.Renderers[0].gameObject.name +
                      " Registry dump:\n" + registry.RegistryDump());
#endif

                return new NodeController(filter, group, node, proxies, new RefCount(), context, registry);
            }
        }

        public async Task<NodeController> Refresh(
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies,
            RenderAspects changes,
            string trace
        )
        {
            var ev = TraceBuffer.RecordTraceEvent(
                "NodeController.Refresh", 
                (ev) => $"NodeController: Refresh for {ev.Arg0} on {ev.Arg1}",
                arg0: _filter,
                arg1: _group
            );

            using (ev.Scope())
            {

                AsyncProfiler.PushProfilerContext("NodeController.Refresh[" + _filter + "]",
                    _group.Renderers[0].gameObject);
                var registry = ObjectRegistry.Merge(null, proxies.Select(p => p.Item3)
                    .Append(ObjectRegistry));
                var context = new ComputeContext("NodeController (refresh) for " + _filter + " on " +
                                                 _group.Renderers[0].gameObject.name);

                IRenderFilterNode node;

                if (changes == 0 && !IsInvalidated)
                {
                    // Reuse the old node in its entirety
                    node = _node;
                    context = _context;
                }
                else
                {
                    using (var scope = new ObjectRegistryScope(registry))
                    {
                        node = await _node.Refresh(
                            proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                            context,
                            changes
                        );
                    }
                }

                RefCount refCount;
                if (node == _node)
                {
                    refCount = _refCount;
                    refCount.Count++;
                }
                else if (node == null)
                {
                    return null;
                }
                else
                {
                    refCount = new RefCount();
                }

                var controller = new NodeController(_filter, _group, node, proxies, refCount, context, registry);
                controller.WhatChanged = changes | node.WhatChanged;

                foreach (var proxy in proxies)
                {
                    proxy.Item2.ChangeFlags |= node.WhatChanged;
                }

                return controller;
            }
        }

        public void Dispose()
        {
            if (--_refCount.Count == 0)
            {
                _node.Dispose();
            }
        }

        public override string ToString()
        {
            return "Node(" + _filter + " for group including " + _group.Renderers[0].gameObject.name + ")";
        }
    }
}