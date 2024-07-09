using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.rq;
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
        
        private class RendererState
        {
            public Renderer InactiveProxy;
            public int ActiveProxyCount = 0;
        }
        
        private Dictionary<Renderer, RendererState> _renderers = new(new ObjectIdentityComparer<Renderer>());
        private bool _cleanupPending = false;
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
                }
                else
                {
                    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                }

                _isRegistered = value;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            // Destroy all cached proxies when entering or exiting play mode
            Dispose();
        }

        public Renderer GetOrCreate(Renderer original, Func<Renderer> create)
        {
            IsRegistered = true;
            
            if (!_renderers.TryGetValue(original, out var state))
            {
                state = new RendererState();
                _renderers.Add(original, state);
            }

            Renderer proxy;
            if (state.InactiveProxy != null)
            {
                proxy = state.InactiveProxy;
                state.InactiveProxy = null;
                state.ActiveProxyCount++;
                proxy.enabled = true;
                return proxy;
            }
            
            proxy = create();
            if (proxy == null)
            {
                return null;
            }

            state.ActiveProxyCount++;
            _proxyObjectInstanceIds.Add(proxy.gameObject.GetInstanceID());
            
            return proxy;
        }
        
        public void ReturnProxy(Renderer original, Renderer proxy)
        {
            IsRegistered = true;
            
            if (!_renderers.TryGetValue(original, out var state))
            {
                Debug.Log("ProxyObjectCache: Renderer not found in cache");
                DestroyProxy(proxy);
                return;
            }

            if (!_cleanupPending)
            {
                EditorApplication.delayCall += Cleanup;
                _cleanupPending = true;
            }

            state.ActiveProxyCount--;
            if (state.ActiveProxyCount > 0 && state.InactiveProxy == null)
            {
                state.InactiveProxy = proxy;
                proxy.enabled = false;
                return;
            }
            
            DestroyProxy(proxy);

            if (state.ActiveProxyCount == 0)
            {
                DestroyProxy(state.InactiveProxy);
                _renderers.Remove(original);
            }
        }

        private static void DestroyProxy(Renderer proxy)
        {
            if (proxy == null) return;
            
            var gameObject = proxy.gameObject;
            _proxyObjectInstanceIds.Remove(gameObject.GetInstanceID());
            Object.DestroyImmediate(gameObject);
        }

        private void Cleanup()
        {
            _cleanupPending = false;
            
            foreach (var entry in _renderers.Where(kv => kv.Key == null).ToList())
            {
                DestroyProxy(entry.Value.InactiveProxy);
                _renderers.Remove(entry.Key);
            }
        }

        public void Dispose()
        {
            foreach (var entry in _renderers)
            {
                DestroyProxy(entry.Value.InactiveProxy);
            }
            _renderers.Clear();
            IsRegistered = false;
        }
    }
}