using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.rq;
using UnityEditor;
using UnityEngine;

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
        
        public Renderer GetOrCreate(Renderer original, Func<Renderer> create)
        {
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
                return proxy;
            }
            
            Debug.Log("=== Creating new proxy for " + original.gameObject.name);
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
            var gameObject = proxy.gameObject;
            _proxyObjectInstanceIds.Remove(gameObject.GetInstanceID());
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private void Cleanup()
        {
            _cleanupPending = false;
            
            foreach (var entry in _renderers.Where(kv => kv.Key == null).ToList())
            {
                if (entry.Value.InactiveProxy != null)
                {
                    UnityEngine.Object.DestroyImmediate(entry.Value.InactiveProxy.gameObject);
                }
                _renderers.Remove(entry.Key);
            }
        }

        public void Dispose()
        {
            foreach (var entry in _renderers)
            {
                if (entry.Value.InactiveProxy != null)
                {
                    UnityEngine.Object.DestroyImmediate(entry.Value.InactiveProxy.gameObject);
                }
            }
            _renderers.Clear();
        }
    }
}