#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEngine;

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
            
            OnFrame();
        }

        internal void OnFrame()
        {
            foreach (var (original, proxy) in _proxies)
            {
                if (original != null && proxy.Renderer != null)
                {
                    _node.OnFrame(original, proxy.Renderer);
                }
            }
        }

        public static Task<NodeController> Create(
            IRenderFilter filter,
            RenderGroup group,
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies)
        {
            return Create(filter, group, ObjectRegistry.Merge(null, proxies.Select(p => p.Item3)), proxies);
        }

        public static async Task<NodeController> Create(
            IRenderFilter filter,
            RenderGroup group,
            ObjectRegistry registry,
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies)
        {
            ComputeContext context = new ComputeContext();

            IRenderFilterNode node;
            using (var scope = new ObjectRegistryScope(registry))
            {
                node = await filter.Instantiate(
                    group,
                    proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                    context
                );
            }

            return new NodeController(filter, group, node, proxies, new RefCount(), context, registry);
        }

        public async Task<NodeController> Refresh(
            List<(Renderer, ProxyObjectController, ObjectRegistry)> proxies,
            RenderAspects changes
        )
        {
            var registry = ObjectRegistry.Merge(null, proxies.Select(p => p.Item3));
            ComputeContext context = new ComputeContext();

            IRenderFilterNode node;

            if (changes == 0 && !IsInvalidated)
            {
                // Reuse the old node in its entirety
                node = _node;
                context = _context;
                Debug.Log("=== Reusing node " + _node);
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

                Debug.Log("=== Refreshing node " + _node + " with changes " + changes + "; success? " + (node != null) + " same? " + (node == _node));
            }

            RefCount refCount;
            if (node == _node)
            {
                refCount = _refCount;
                refCount.Count++;
            }
            else if (node == null)
            {
                return await Create(_filter, _group, registry, proxies);
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