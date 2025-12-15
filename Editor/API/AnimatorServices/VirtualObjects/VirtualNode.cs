#nullable enable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     Base class for all virtual animation nodes. Contains common functionality for cache invalidation.
    ///     Generally, external libraries should not use this class directly.
    /// </summary>
    [PublicAPI]
    public abstract class VirtualNode
    {
        private Action? _lastCacheObserver;

        internal VirtualNode()
        {
        }

        internal void Invalidate()
        {
            _lastCacheObserver?.Invoke();
            _lastCacheObserver = null;
        }

        internal T I<T>(T val)
        {
            Invalidate();
            return val;
        }

        internal void RegisterCacheObserver(Action? observer)
        {
            if (observer != _lastCacheObserver && _lastCacheObserver != null)
            {
                _lastCacheObserver.Invoke();
            }

            _lastCacheObserver = observer;
        }
        
        /// <summary>
        /// The original unity object that this node represents. Used in ToString debug output.
        /// </summary>
        protected Object? OriginalObject { get; set; }

        // The name of the associated unity object. 
        public abstract string Name { get; set; }

        public override string ToString()
        {
            // Always include the runtime type
            var typeName = GetType().Name;
            
            var assetPart = "";
            string assetName = "";
            try
            {
                assetName = OriginalObject?.name ?? "";
                if (!string.IsNullOrEmpty(assetName)) assetPart = $"asset:{assetName}";
            }
            catch (Exception)
            {
                assetPart = "asset:[destroyed]";
            }
            
            var namePart = "";
            string virtualNodeName = "";
            try
            {
                virtualNodeName = Name;
                if (!string.IsNullOrEmpty(virtualNodeName)) namePart = $"current:{virtualNodeName}";
            }
            catch (Exception)
            {
                namePart = "current:[destroyed]";
            }

            if (assetPart.Length > 0 && namePart.Length > 0 && virtualNodeName != assetName)
            {
                return $"[{typeName} {assetPart} renamedTo:{virtualNodeName}]";
            }

            if (assetPart.Length > 0) return $"[{typeName} {assetPart}]";
            if (namePart.Length > 0) return $"[{typeName} {namePart}]";

            return $"[{typeName}]";
        }

        public IEnumerable<VirtualNode> AllReachableNodes()
        {
            // node -> source
            var visited = new Dictionary<VirtualNode, VirtualNode?>();
            var queue = new Queue<VirtualNode>();
            
            queue.Enqueue(this);
            visited[this] = null;

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                yield return node;

                foreach (var child in node.EnumerateChildren())
                {
                    if (child == null)
                    {
                        // Trace origin
                        List<string> trace = new();
                        var pointer = node;

                        while (pointer != null)
                        {
                            trace.Add(pointer.ToString());
                            pointer = visited[pointer];
                        }

                        // Print debug message
                        trace.Reverse();
                        Debug.LogWarning("[NDMF VirtualNode.AllReachableNodes] Null child node in " +
                                         string.Join(" -> ", trace));
                        continue;
                    }

                    if (!visited.ContainsKey(child))
                    {
                        visited[child] = node;
                        queue.Enqueue(child);
                    }
                }
            }
        }
        
        public IEnumerable<VirtualNode> EnumerateChildren()
        {
            return _EnumerateChildren();
        }

        protected virtual IEnumerable<VirtualNode> _EnumerateChildren()
        {
            yield break;
        }
        
        
    }
}
