#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using nadena.dev.ndmf.preview.trace;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.preview
{
    public static class PropCacheDebug
    {
        private static readonly ConditionalWeakTable<object, Action> _globalInvalidateCallbacks = new();

        internal static void InternalRegister(object cache, Action invalidateMe)
        {
            _globalInvalidateCallbacks.Add(cache, invalidateMe);
        }

        /// <summary>
        ///     Invalidates all values in all PropCaches.
        /// </summary>
        [PublicAPI]
        public static void InvalidateAllCaches()
        {
            foreach (var entry in _globalInvalidateCallbacks)
            {
                entry.Value();
            }
        }
    }

    /// <summary>
    ///     Caches the result of a computation, and invalidates based on ComputeContext invalidation rules.
    ///     This class allows you to cache the result of a function from TKey to TValue, where the function
    ///     observes values using a ComputeContext. When the ComputeContext is invalidated, the cache entry will
    ///     be cleared, and any downstream observers will be invalidated as well.
    /// 
    ///     Note that this cache currently invalidates values only when the ComputeContext is invalidated;
    ///     in particular, if TKey is a unity object which is destroyed, this in itself will not result in the
    ///     associated value being freed from memory.
    /// 
    ///     This class is not thread-safe; all calls must be made from the Unity main thread.
    ///     (This may change in the future)
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    [PublicAPI]
    public sealed class PropCache<TKey, TValue>
    {
        private class CacheEntry
        {
            public ComputeContext GenerateContext;
            public readonly ComputeContext ObserverContext;
            public readonly PropCache<TKey, TValue> Owner;
            public readonly TKey Key;
            public TValue? Value;
            public readonly string DebugName;
            public readonly int Generation;

            public CacheEntry(
                PropCache<TKey, TValue> owner,
                TKey key,
                int generation
            )
            {
                Owner = owner;
                Key = key;
                Generation = generation;
                DebugName = Owner._debugName;

                var formattedKey = FormatKey(key);
                GenerateContext =
                    new ComputeContext("PropCache/" + DebugName + " key " + formattedKey + " gen=" + Generation);
                ObserverContext = new ComputeContext("Observer for PropCache/" + DebugName + " for key " +
                                                     formattedKey + " gen=" + Generation);
            }
        }

        private readonly string _debugName;
        private readonly Func<ComputeContext, TKey, TValue> _operator;
        private readonly Func<TValue, TValue, bool>? _equalityComparer;
        private readonly Dictionary<TKey, CacheEntry> _cache = new();

        // This is used only for debugging purposes to identify when the propcache is regenerated,
        // we don't mind it not being shared across different instantiations.
        // ReSharper disable once StaticMemberInGenericType
        private static int _generation;

        /// <summary>
        ///     Creates a new propcache
        /// </summary>
        /// <param name="debugName">The debug name, which is displayed in the preview trace and other debug contexts</param>
        /// <param name="operatorFunc">The function to cache the result of</param>
        /// <param name="equalityComparer">
        ///     If not null, a function used to determine if the cached function changed its
        ///     result. If this function returns true, then downstream consumers will not be invalidated. Note that if the
        ///     equality comparator is present, the function may be re-evaluated multiple times per cache invalidation.
        /// </param>
        public PropCache(
            string debugName,
            Func<ComputeContext, TKey, TValue> operatorFunc,
            Func<TValue, TValue, bool>? equalityComparer = null
        )
        {
            _debugName = debugName;
            _operator = operatorFunc;
            _equalityComparer = equalityComparer;

            WeakReference<PropCache<TKey, TValue>> selfRef = new(this);
            PropCacheDebug.InternalRegister(this, () =>
            {
                if (selfRef.TryGetTarget(out var target))
                {
                    target.InvalidateAll();
                }
            });
        }

        public void InvalidateAll()
        {
            foreach (var entry in _cache.Values)
            {
                entry.ObserverContext.Invalidate();
            }

            _cache.Clear();
        }

        private static void InvalidateEntry(CacheEntry entry)
        {
            var newGenContext = new ComputeContext("PropCache/" + entry.DebugName + " key " + FormatKey(entry.Key) +
                                                   " gen=" + _generation++);
            if (entry.Owner._equalityComparer != null && !entry.ObserverContext.IsInvalidated)
            {
                var newValue = entry.Owner._operator(newGenContext, entry.Key);
                if (entry.Owner._equalityComparer(entry.Value!, newValue))
                {
                    TraceBuffer.RecordTraceEvent(
                        "PropCache.InvalidateEntry",
                        ev => $"[PropCache/{ev.Arg0}] Value did not change, retaining result (new gen={ev.Arg1})",
                        entry.DebugName, entry.Generation
                    );

                    entry.GenerateContext = newGenContext;
                    entry.GenerateContext.InvokeOnInvalidate(entry, InvalidateEntry);
                    return;
                }
            }

            var trace = TraceBuffer.RecordTraceEvent(
                "PropCache.InvalidateEntry",
                ev => $"[PropCache/{ev.Arg0}] Value changed, invalidating",
                entry.DebugName
            );

            // TODO: we discard the above speculative calculation in order to ensure that we can delete entries from the
            // cache. Consider storing the new value for a few frames just to see if it'll actually be queried again.
            entry.Owner._cache.Remove(entry.Key);
            using (trace.Scope())
            {
                entry.ObserverContext.Invalidate();
            }
        }

        /// <summary>
        ///     Fetches a value from the cache, computing it if necessary.
        /// </summary>
        /// <param name="context">The compute context to use to observe the value</param>
        /// <param name="key">The key to look up</param>
        /// <returns>The computed value</returns>
        public TValue Get(ComputeContext context, TKey key)
        {
            TraceEvent ev;
            if (!_cache.TryGetValue(key, out var entry) || entry.GenerateContext.IsInvalidated)
            {
                var curGen = _generation++;

                entry = new CacheEntry(this, key, curGen);

                ev = TraceBuffer.RecordTraceEvent(
                    "PropCache.Get",
                    ev2 =>
                    {
                        // ReSharper disable once InconsistentNaming
                        var entry2 = (CacheEntry)ev2.Arg0;
                        return
                            $"[PropCache/{entry2.DebugName}] Cache miss for key {entry2.Key} gen={entry2.Generation} from context {ev2.Arg1}";
                    },
                    entry, context
                );

                _cache[key] = entry;
                using (ev.Scope())
                {
                    entry.Value = _operator(entry.GenerateContext, key);
                    entry.GenerateContext.InvokeOnInvalidate(entry, InvalidateEntry);
                }
            }
            else
            {
                TraceBuffer.RecordTraceEvent(
                    "PropCache.Get",
                    ev2 =>
                    {
                        var entry2 = (CacheEntry)ev2.Arg0;
                        return
                            $"[PropCache/{entry2.DebugName}] Cache hit for key {entry2.Key} gen={entry2.Generation} from context {ev2.Arg1}";
                    },
                    entry, context
                );
            }

            entry.ObserverContext.Invalidates(context);

            return entry.Value!;
        }

        private static string FormatKey(object? obj)
        {
            if (obj is Object unityObj)
            {
                return $"{unityObj.GetHashCode()}#{unityObj}";
            }

            return "" + obj;
        }
    }
}