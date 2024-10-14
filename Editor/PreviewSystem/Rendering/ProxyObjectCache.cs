using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.preview
{
    internal class ProxyObjectCache : IDisposable
    {
        private static HashSet<int> _proxyObjectInstanceIds = new();
        
        public static bool IsProxyObject(GameObject obj)
        {
            if (obj == null) return false;

            return _proxyObjectInstanceIds.Contains(obj.GetInstanceID());
        }

        public interface IProxyHandle : IDisposable
        {
            public Renderer PrimaryProxy { get; }
            public Renderer GetSetupProxy();
            public void ReturnSetupProxy(Renderer proxy);
        }

        private class ProxyHandleImpl : IProxyHandle
        {
            private readonly ProxyObjectCache _cache;
            private readonly Renderer _key;
            private readonly Func<Renderer> _createFunc;
            private readonly RendererState _state;

            public ProxyHandleImpl(ProxyObjectCache cache, Renderer key, Func<Renderer> createFunc, RendererState state)
            {
                _cache = cache;
                _key = key;
                _createFunc = createFunc;
                _state = state;
            }

            public Renderer PrimaryProxy => _state.PrimaryProxy;

            public Renderer GetSetupProxy()
            {
                if (_state.InactiveSetupProxy != null)
                {
                    var proxy = _state.InactiveSetupProxy;
                    proxy.enabled = true;
                    _state.InactiveSetupProxy = null;
                    return proxy;
                }

                return _createFunc();
            }

            public void ReturnSetupProxy(Renderer proxy)
            {
                if (_state.InactiveSetupProxy != null || _state.ActivePrimaryCount == 0)
                {
                    DestroyProxy(proxy);
                }
                else
                {
                    proxy.enabled = false;
                    _state.InactiveSetupProxy = proxy;
                }
            }

            public void Dispose()
            {
                if (--_state.ActivePrimaryCount == 0)
                {
                    _cache.MaybeDisposeProxy(_key);
                }
            }
        }

        private class RendererState
        {
            public Renderer PrimaryProxy;
            public int ActivePrimaryCount;

            public Renderer InactiveSetupProxy;
        }

        private readonly Dictionary<Renderer, RendererState> _renderers = new();
        private bool _isRegistered = false;

        private bool IsRegistered
        {
            get => _isRegistered;
            set
            {
                if (_isRegistered == value) return;
                if (value)
                {
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    EditorApplication.update += Cleanup;
                }
                else
                {
                    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                    EditorApplication.update -= Cleanup;
                }

                _isRegistered = value;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            // Destroy all cached proxies when entering or exiting play mode
            Dispose();
        }

        public IProxyHandle GetHandle(Renderer original, Func<Renderer> create)
        {
            IsRegistered = true;
            
            if (!_renderers.TryGetValue(original, out var state))
            {
                state = new RendererState();
                state.PrimaryProxy = create();
                _renderers.Add(original, state);
            }

            state.ActivePrimaryCount++;

            return new ProxyHandleImpl(this, original, () =>
            {
                var newProxy = create();
                _proxyObjectInstanceIds.Add(newProxy.gameObject.GetInstanceID());

                return newProxy;
            }, state);
        }

        private static void DestroyProxy(Renderer proxy)
        {
            if (proxy == null) return;
            
            var gameObject = proxy.gameObject;
            _proxyObjectInstanceIds.Remove(gameObject.GetInstanceID());
            Object.DestroyImmediate(gameObject);
        }


        private void MaybeDisposeProxy(Renderer key)
        {
            if (_renderers.TryGetValue(key, out var state) && state.ActivePrimaryCount == 0)
            {
                DestroyProxy(state.PrimaryProxy);
                DestroyProxy(state.InactiveSetupProxy);
                _renderers.Remove(key);
            }
        }
        
        private void Cleanup()
        {
            foreach (var entry in _renderers.Where(kv => kv.Key == null).ToList())
            {
                DestroyProxy(entry.Value.InactiveSetupProxy);
                DestroyProxy(entry.Value.PrimaryProxy);
                _renderers.Remove(entry.Key);
            }
        }

        public void Dispose()
        {
            foreach (var entry in _renderers)
            {
                DestroyProxy(entry.Value.InactiveSetupProxy);
                DestroyProxy(entry.Value.PrimaryProxy);
            }
            _renderers.Clear();
            IsRegistered = false;
        }
    }
}