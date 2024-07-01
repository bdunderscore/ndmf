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
    internal class ProxySession : IDisposable
    {
        private ProxyPipeline _active, _next;

        private IDisposable _unsubscribe;

        internal ImmutableDictionary<Renderer, Renderer> OriginalToProxyRenderer =>
            _active?.OriginalToProxyRenderer ?? ImmutableDictionary<Renderer, Renderer>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> OriginalToProxyObject =>
            _active?.OriginalToProxyObject ?? ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> ProxyToOriginalObject =>
            _active?.ProxyToOriginalObject ?? ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ProxyObjectCache _proxyCache = new();

        private ImmutableList<IRenderFilter> _filters;
        public ImmutableList<IRenderFilter> Filters
        {
            get => _filters;
            set
            {
                if (_filters != null && _filters.SequenceEqual(value)) return;
                
                _active?.Invalidate();
                _next?.Invalidate();

                _filters = value;
            } 
        }
        
        public ProxySession(ImmutableList<IRenderFilter> filters)
        {
            Filters = filters;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
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

            if (activeNeedsReplacement && _next == null)
            {
                _next = new ProxyPipeline(_proxyCache, _filters.ToList(), _active);
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