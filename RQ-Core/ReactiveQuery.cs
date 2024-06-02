#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    /// <summary>
    /// Represents a mapping from T to ReactiveValue<U>, which is computed and updated asynchronously.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U"></typeparam>
    public sealed class ReactiveQuery<T, U> where T : class
    {
        private readonly object _lock = new object();
        private readonly string _description;
        private readonly Func<ComputeContext, T, Task<U>> _compute;
        private readonly Dictionary<WeakKey<T>, WeakReference<CacheValue<T, U>>> _cache = new();

        private int additions = 0;

        public ReactiveQuery(string description, Func<ComputeContext, T, Task<U>> compute)
        {
            _compute = compute;
            _description = description;
        }

        public ReactiveValue<U> Get(T key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(new WeakKey<T>(key), out var weakQuery))
                {
                    if (weakQuery.TryGetTarget(out var query))
                    {
                        return query;
                    }
                }

                var newQuery = new CacheValue<T, U>(key, _compute, _description);
                _cache[new WeakKey<T>(key)] = new WeakReference<CacheValue<T, U>>(newQuery);
                additions++;

                if (additions > _cache.Count / 2)
                {
                    PruneCache();
                }

                return newQuery;
            }
        }

        private void PruneCache()
        {
            lock (_lock)
            {
                additions = 0;

                var keysToRemove = new List<WeakKey<T>>();
                foreach (var pair in _cache)
                {
                    if (!pair.Value.TryGetTarget(out _))
                    {
                        keysToRemove.Add(pair.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }
            }
        }

        private class WeakKey<T> where T : class
        {
            private readonly WeakReference<T> _key;
            private int _hashCode;

            public WeakKey(T key)
            {
                _key = new WeakReference<T>(key);
                _hashCode = key.GetHashCode();
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            public override bool Equals(object obj)
            {
                if (this == obj) return true;

                if (obj is WeakKey<T> other)
                {
                    if (_key.TryGetTarget(out var key) && other._key.TryGetTarget(out var otherKey))
                    {
                        return key.Equals(otherKey);
                    }
                }

                return false;
            }
        }

        private class CacheValue<T, U> : ReactiveValue<U>
        {
            private readonly string _description;
            private readonly T _key;
            private readonly Func<ComputeContext, T, Task<U>> _compute;

            public CacheValue(T key, Func<ComputeContext, T, Task<U>> compute, string description)
            {
                _key = key;
                _compute = compute;
                _description = description + " for " + key;
            }

            protected override Task<U> Compute(ComputeContext context)
            {
                return _compute(context, _key);
            }

            public override string ToString()
            {
                return _description;
            }
        }
    }
}