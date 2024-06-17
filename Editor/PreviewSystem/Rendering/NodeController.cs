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

        internal Task OnInvalidate => _context.OnInvalidate;

        internal ProxyObjectController GetProxyFor(Renderer r)
        {
            return _proxies.Find(p => p.Item1 == r).Item2;
        }

        private NodeController(
            RenderGroup group,
            IRenderFilterNode node,
            List<(Renderer, ProxyObjectController)> proxies,
            RefCount refCount,
            ComputeContext context
        )
        {
            _group = group;
            _node = node;
            _proxies = proxies;
            _refCount = refCount;
            _context = context;
            
            OnFrame();

            ///OnInvalidate.ContinueWith(_ => Debug.Log("=== Node invalidated: " + _node.ToString()));
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

        public static async Task<NodeController> Create(
            IRenderFilter filter,
            RenderGroup group,
            List<(Renderer, ProxyObjectController)> proxies)
        {
            ComputeContext context = new ComputeContext(() => filter.ToString());
         
            var node = await filter.Instantiate(
                group,
                proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                context
            );

            return new NodeController(group, node, proxies, new RefCount(), context);
        }

        public async Task<NodeController> Refresh(
            List<(Renderer, ProxyObjectController)> proxies,
            RenderAspects changes
        )
        {
            ComputeContext context = new ComputeContext(() => _node.ToString());

            var node = await _node.Refresh(
                proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                context,
                changes
            );

            RefCount refCount;
            if (node == _node)
            {
                refCount = _refCount;
                refCount.Count++;
            }
            else if (node == null)
            {
                return await Create(_filter, _group, proxies);
            }
            else
            {
                refCount = new RefCount();
            }

            var controller = new NodeController(_group, node, proxies, refCount, context);
            controller.WhatChanged = changes | node.WhatChanged;
            return controller;
        }

        public void Dispose()
        {
            if (--_refCount.Count == 0)
            {
                _node.Dispose();
            }
        }
    }
}