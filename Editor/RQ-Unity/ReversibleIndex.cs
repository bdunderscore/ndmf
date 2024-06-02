#region

using System;
using System.Collections.Generic;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    internal class ReversibleIndex<K, V>
    {
        private Dictionary<K, V> _forward = new Dictionary<K, V>();
        private Dictionary<V, HashSet<K>> _reverse = new Dictionary<V, HashSet<K>>();

        public void Set(K key, V value)
        {
            if (_forward.TryGetValue(key, out var oldValue))
            {
                var prior_rev = _reverse[oldValue];
                prior_rev.Remove(key);
                if (prior_rev.Count == 0)
                {
                    _reverse.Remove(oldValue);
                }
            }
            else
            {
                _forward[key] = value;
            }

            if (!_reverse.TryGetValue(value, out var keys))
            {
                keys = new HashSet<K>();
                _reverse[value] = keys;
            }

            keys.Add(key);
        }

        public bool TryGet(K key, out V value)
        {
            return _forward.TryGetValue(key, out value);
        }

        public IEnumerable<K> GetKeys(V value)
        {
            if (_reverse.TryGetValue(value, out var set))
            {
                return set;
            }
            else
            {
                return Array.Empty<K>();
            }
        }

        public void Remove(K key)
        {
            if (_forward.TryGetValue(key, out var value))
            {
                _forward.Remove(key);
                var keys = _reverse[value];
                keys.Remove(key);
                if (keys.Count == 0)
                {
                    _reverse.Remove(value);
                }
            }
        }

        public void Clear()
        {
            _forward.Clear();
            _reverse.Clear();
        }
    }
}