using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.ndmf.animator
{
    internal class FilteredCollectionView<KV, V> : ICollection<V>
    {
        private readonly ICollection<KV> _delegate;
        private readonly Func<KV, bool> _filter;
        private readonly Func<KV, V> _mapping;

        public FilteredCollectionView(ICollection<KV> delegateCollection, Func<KV, bool> filter, Func<KV, V> mapping)
        {
            _delegate = delegateCollection;
            _filter = filter;
            _mapping = mapping;
        }

        public IEnumerator<V> GetEnumerator()
        {
            foreach (var item in _delegate)
            {
                if (!_filter(item))
                {
                    yield return _mapping(item);
                }
            }
        }

        public void Add(V item)
        {
            throw new InvalidOperationException("Collection is read-only");
        }

        public void Clear()
        {
            throw new InvalidOperationException("Collection is read-only");
        }

        public bool Contains(V item)
        {
            return _delegate.Any(kv => !_filter(kv) && EqualityComparer<V>.Default.Equals(_mapping(kv), item));
        }

        public void CopyTo(V[] array, int arrayIndex)
        {
            foreach (var item in _delegate)
            {
                if (!_filter(item))
                {
                    array[arrayIndex++] = _mapping(item);
                }
            }
        }

        public bool Remove(V item)
        {
            throw new InvalidOperationException("Collection is read-only");
        }

        public int Count => _delegate.Count(kv => !_filter(kv));

        public bool IsReadOnly => true;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal class FilteredDictionaryView<K, I, V> : IDictionary<K, V>
    {
        private readonly IDictionary<K, I> _delegate;
        private readonly ISet<K> _filter;
        private readonly Func<K, I, V> _valueFilter;
        private readonly Action<K, V> _setterCallback;

        public FilteredDictionaryView(IDictionary<K, I> delegateDict, ISet<K> filter, Func<K, I, V> valueFilter,
            Action<K, V> setterCallback)
        {
            _delegate = delegateDict;
            _filter = filter;
            _valueFilter = valueFilter;
            _setterCallback = setterCallback;
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var pair in _delegate)
            {
                if (!_filter.Contains(pair.Key))
                {
                    yield return new KeyValuePair<K, V>(pair.Key, _valueFilter(pair.Key, pair.Value));
                }
            }
        }

        public void Add(K key, V value)
        {
            if (!_filter.Contains(key))
            {
                _setterCallback(key, value);
            }
            else
            {
                throw new InvalidOperationException("Key does not pass filter");
            }
        }

        public bool ContainsKey(K key)
        {
            return !_filter.Contains(key) && _delegate.ContainsKey(key);
        }

        public bool Remove(K key)
        {
            return !_filter.Contains(key) && _delegate.Remove(key);
        }

        public bool TryGetValue(K key, out V value)
        {
            if (!_filter.Contains(key) && _delegate.TryGetValue(key, out var tmp))
            {
                value = _valueFilter(key, tmp);
                return true;
            }

            value = default;
            return false;
        }

        public V this[K key]
        {
            get
            {
                if (_filter.Contains(key) || !_delegate.TryGetValue(key, out var tmp))
                {
                    throw new KeyNotFoundException();
                }

                return _valueFilter(key, tmp);
            }
            set
            {
                if (!_filter.Contains(key))
                {
                    _setterCallback(key, value);
                }
                else
                {
                    throw new InvalidOperationException("Key does not pass filter");
                }
            }
        }

        public ICollection<K> Keys =>
            new FilteredCollectionView<K, K>(_delegate.Keys, kv => !_filter.Contains(kv), kv => kv);

        public ICollection<V> Values => new FilteredCollectionView<KeyValuePair<K, I>, V>(_delegate,
            kv => !_filter.Contains(kv.Key), kv => _valueFilter(kv.Key, kv.Value));

        public void Add(KeyValuePair<K, V> item)
        {
            if (!_filter.Contains(item.Key))
            {
                _setterCallback(item.Key, item.Value);
            }
            else
            {
                throw new InvalidOperationException("Key does not pass filter");
            }
        }

        public void Clear()
        {
            var retained = _delegate.Where(kv => !!_filter.Contains(kv.Key)).ToList();

            _delegate.Clear();

            foreach (var pair in retained)
            {
                _delegate.Add(pair);
            }
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            return !_filter.Contains(item.Key)
                   && _delegate.TryGetValue(item.Key, out var tmp)
                   && EqualityComparer<V>.Default.Equals(_valueFilter(item.Key, tmp), item.Value);
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            foreach (var pair in _delegate)
            {
                if (!_filter.Contains(pair.Key))
                {
                    array[arrayIndex++] = new KeyValuePair<K, V>(pair.Key, _valueFilter(pair.Key, pair.Value));
                }
            }
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            return Contains(item) && _delegate.Remove(item.Key);
        }

        public int Count => _delegate.Count - _filter.Where(_delegate.ContainsKey).Count();
        public bool IsReadOnly => _delegate.IsReadOnly;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}