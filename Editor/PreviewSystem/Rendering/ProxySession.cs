#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf.rq;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class ProxySession : IObserver<ImmutableList<IRenderFilter>>, IDisposable
    {
        private ReactiveValue<ImmutableList<IRenderFilter>> _filters;
        private NodeGraph _graph = new();
        private ProxyPipeline _active, _next;

        private IDisposable _unsubscribe;

        private Dictionary<Renderer, ProxyObjectController> _proxyControllers = new();
        private List<(Renderer, Renderer)> activeRenderers = new();

        internal ImmutableDictionary<Renderer, Renderer> OriginalToProxyRenderer =
            ImmutableDictionary<Renderer, Renderer>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> OriginalToProxyObject =
            ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> ProxyToOriginalObject =
            ImmutableDictionary<GameObject, GameObject>.Empty;

        public ProxySession(ReactiveValue<ImmutableList<IRenderFilter>> filters)
        {
            _filters = filters;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _unsubscribe = filters.Subscribe(this);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            _active = _next = null;
            _graph.Retain(ImmutableHashSet<ProxyNodeKey>.Empty);
        }

        public void Dispose()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _unsubscribe?.Dispose();
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Debug.LogException(error);
        }

        public void OnNext(ImmutableList<IRenderFilter> filters)
        {
            _active?.Invalidate();
            _next?.Invalidate();
        }

        public IEnumerable<(Renderer, Renderer)> OnPreCull()
        {
            if (_active == null || _active.Invalidated)
            {
                if (_next != null)
                {
                    if (_next.Aborted) _next = null;
                    else if (_next.BuildCompleted)
                    {
                        _active = _next;
                        _next = null;
                        CollectNodes();
                        RebuildRenderers();
                    }
                }
            }

            if (_active == null || _active.Invalidated)
            {
                if (_next == null && _filters.TryGetValue(out var list))
                {
                    _next = new ProxyPipeline(_graph, list);
                }
            }

            if (_active is not { BuildCompleted: true })
            {
                return Array.Empty<(Renderer, Renderer)>();
            }

            foreach (var poc in _proxyControllers.Values)
            {
                poc.OnPreCull();
            }

            return activeRenderers;
        }

        private void RebuildRenderers()
        {
            Dictionary<Renderer, ProxyObjectController> retain = new();

            activeRenderers.Clear();

            var originalToProxyObject = ImmutableDictionary<GameObject, GameObject>.Empty.ToBuilder();
            var proxyToOriginalObject = ImmutableDictionary<GameObject, GameObject>.Empty.ToBuilder();
            var originalToProxyRenderer = ImmutableDictionary<Renderer, Renderer>.Empty.ToBuilder();

            foreach (var srcRenderer in _active.Renderers)
            {
                if (_proxyControllers.TryGetValue(srcRenderer, out var poc) && poc.IsValid)
                {
                    retain.Add(srcRenderer, poc);
                }
                else
                {
                    poc = new ProxyObjectController(srcRenderer);
                    retain.Add(srcRenderer, poc);
                }

                poc.Pipeline = _active;
                activeRenderers.Add((srcRenderer, poc.Renderer));

                originalToProxyObject[srcRenderer.gameObject] = poc.Renderer.gameObject;
                proxyToOriginalObject[poc.Renderer.gameObject] = srcRenderer.gameObject;
                originalToProxyRenderer[srcRenderer] = poc.Renderer;
            }

            _proxyControllers = retain;

            OriginalToProxyObject = originalToProxyObject.ToImmutable();
            ProxyToOriginalObject = proxyToOriginalObject.ToImmutable();
            OriginalToProxyRenderer = originalToProxyRenderer.ToImmutable();
        }

        private void CollectNodes()
        {
            var toRetain = new HashSet<ProxyNodeKey>();

            _active?.CollectNodes(toRetain);
            _next?.CollectNodes(toRetain);

            _graph.Retain(toRetain);
        }
    }
}