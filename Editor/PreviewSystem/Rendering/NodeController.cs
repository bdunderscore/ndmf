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

        // ReSharper disable once NotAccessedField.Local
        private readonly ComputeContext _context; // prevents GC
        internal ulong WhatChanged = IRenderFilterNode.Everything;
        internal Task OnInvalidate;

        internal ProxyObjectController GetProxyFor(Renderer r)
        {
            return _proxies.Find(p => p.Item1 == r).Item2;
        }

        private NodeController(
            RenderGroup group,
            IRenderFilterNode node,
            List<(Renderer, ProxyObjectController)> proxies,
            RefCount refCount,
            Task invalidated,
            ComputeContext context
        )
        {
            _group = group;
            _node = node;
            _proxies = proxies;
            _refCount = refCount;
            _context = context;

            OnInvalidate = invalidated;

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
            RenderGroup group,
            List<(Renderer, ProxyObjectController)> proxies)
        {
            var invalidater = new TaskCompletionSource<object>();

            ComputeContext context = new ComputeContext(() => filter.ToString());
            context.Invalidate = () => invalidater.TrySetResult(null);
            context.OnInvalidate = invalidater.Task;

            var node = await filter.Instantiate(
                group,
                proxies.Select(p => (p.Item1, p.Item2.Renderer)),
                context
            );

            return new NodeController(group, node, proxies, new RefCount(), invalidater.Task, context);
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
                return await Create(_filter, _group, proxies);
            }
            else
            {
                refCount = new RefCount();
            }

            var controller = new NodeController(_group, node, proxies, refCount, invalidater.Task, context);
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