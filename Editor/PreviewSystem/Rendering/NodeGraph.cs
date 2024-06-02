#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class NodeGraph
    {
        private Dictionary<ProxyNodeKey, ProxyNode> _nodes = new();

        public ProxyNode GetOrCreate(ProxyNodeKey key, Func<ProxyNode> OnMissing)
        {
            if (!_nodes.TryGetValue(key, out var node) || node.Invalidated || node.PrepareTask.IsFaulted)
            {
                node = OnMissing();
                _nodes[key] = node;
            }

            return node;
        }

        public void Retain(ISet<ProxyNodeKey> nodesToRetain)
        {
            foreach (var key in _nodes.Keys.ToList())
            {
                if (!nodesToRetain.Contains(key))
                {
                    var node = _nodes[key];
                    _nodes.Remove(key);

                    node.Dispose();
                }
            }
        }
    }
}