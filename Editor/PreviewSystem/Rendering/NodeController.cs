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

        private readonly IRenderFilter _filter;
        private readonly IRenderFilterNode _node;
        private readonly List<(Renderer, ProxyObjectController)> _proxies;
        private readonly RefCount _refCount;
        internal ulong WhatChanged = IRenderFilterNode.Everything;

        internal ProxyObjectController GetProxyFor(Renderer r)
        {
            return _proxies.Find(p => p.Item1 == r).Item2;
        }

        private NodeController(
            IRenderFilterNode node,
            List<(Renderer, ProxyObjectController)> proxies,
            RefCount refCount
        )
        {
            _node = node;
            _proxies = proxies;
            _refCount = refCount;

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

        public static async Task<NodeController> Create(
            IRenderFilter filter,
            List<(Renderer, ProxyObjectController)> proxies)
        {
            var invalidater = new TaskCompletionSource<object>();

            ComputeContext context = new ComputeContext(() => filter.ToString());
            context.Invalidate = () => invalidater.TrySetResult(null);
            context.OnInvalidate = invalidater.Task;

            var node = await filter.Instantiate(
                proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                context
            );

            return new NodeController(node, proxies, new RefCount());
        }

        public async Task<NodeController> Refresh(
            List<(Renderer, ProxyObjectController)> proxies,
            ulong changes
        )
        {
            var invalidater = new TaskCompletionSource<object>();

            ComputeContext context = new ComputeContext(() => _node.ToString());
            context.Invalidate = () => invalidater.SetResult(null);
            context.OnInvalidate = invalidater.Task;

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
                return await Create(_filter, proxies);
            }
            else
            {
                refCount = new RefCount();
            }

            var controller = new NodeController(node, proxies, refCount);
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