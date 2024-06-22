#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.rq;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class ProxySession : IObserver<ImmutableList<IRenderFilter>>, IDisposable
    {
        private ReactiveValue<ImmutableList<IRenderFilter>> _filters;
        private ProxyPipeline _active, _next;

        private IDisposable _unsubscribe;

        internal ImmutableDictionary<Renderer, Renderer> OriginalToProxyRenderer =>
            _active?.OriginalToProxyRenderer ?? ImmutableDictionary<Renderer, Renderer>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> OriginalToProxyObject =>
            _active?.OriginalToProxyObject ?? ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> ProxyToOriginalObject =>
            _active?.ProxyToOriginalObject ?? ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ProxyObjectCache _proxyCache = new();
        
        public ProxySession(ReactiveValue<ImmutableList<IRenderFilter>> filters)
        {
            _filters = filters;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _unsubscribe = filters.Subscribe(this);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            Reset();
        }

        private void Reset()
        {
            _active?.Dispose();
            _next?.Dispose();
            _active = _next = null;
        }

        public void Dispose()
        {
            Reset();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _unsubscribe?.Dispose();
            _proxyCache.Dispose();
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
            bool activeIsReady = _active?.IsReady == true;
            bool activeNeedsReplacement = _active?.IsInvalidated != false;

            if (_next?.IsFailed == true)
            {
                _next.ShowError();
                _next?.Dispose();
                _next = null;
            }

            if (activeNeedsReplacement && _next == null && _filters.TryGetValue(out var filters))
            {
                _next = new ProxyPipeline(_proxyCache, filters.ToList(), _active);
            }

            if (activeNeedsReplacement && _next?.IsReady == true)
            {
                _active?.Dispose();
                _active = _next;
                _next = null;
            }

            if (activeIsReady)
            {
                _active.OnFrame();
                return _active.Renderers;
            }
            else
            {
                return Array.Empty<(Renderer, Renderer)>();
            }
        }
    }
}