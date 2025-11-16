using System;
using System.Collections.Generic;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.PropCacheTest
{
    public class PropCacheTest
    {
        [Test]
        public void TestCacheInvalidation()
        {
            int seq = 0;

            Dictionary<int, List<WeakReference<ComputeContext>>> invalidators = new();
            PropCache<int, int> cache = new PropCache<int, int>("test", (ctx, k) =>
            {
                Debug.Log("Generating value for " + k);
                if (!invalidators.TryGetValue(k, out var list))
                {
                    list = new List<WeakReference<ComputeContext>>();
                    invalidators[k] = list;
                }
                
                list.Add(new WeakReference<ComputeContext>(ctx));
                
                return (k * 10) + seq++;
            });
            
            ComputeContext ctx = new ComputeContext("c1");
            int val = cache.Get(ctx, 1); // 10
            Assert.AreEqual(10, val);
            
            ComputeContext ctx2 = new ComputeContext("c2");
            val = cache.Get(ctx2, 1);    // 10
            Assert.AreEqual(10, val);

            ComputeContext ctx3 = new ComputeContext("c3");
            cache.Get(ctx3, 2);          // 21

            invalidators[1][0].TryGetTarget(out var target);
            target?.Invalidate();
            
            ComputeContext.FlushInvalidates();

            Assert.IsTrue(ctx.IsInvalidated);
            Assert.IsTrue(ctx2.IsInvalidated);
            Assert.IsFalse(ctx3.IsInvalidated);
            
            val = cache.Get(ctx, 1);
            Assert.AreEqual(12, val);
        }

        [Test]
        public void TestEqualityComparator_RetainsOnEqual()
        {
            // If the equality comparator reports that the new value is equal, the observer contexts
            // should NOT be invalidated and the cached value should be retained.
            Dictionary<int, List<WeakReference<ComputeContext>>> invalidators = new();

            PropCache<int, int> cache = new PropCache<int, int>(
                "eqTest",
                (ctx, k) =>
                {
                    if (!invalidators.TryGetValue(k, out var list))
                    {
                        list = new List<WeakReference<ComputeContext>>();
                        invalidators[k] = list;
                    }

                    list.Add(new WeakReference<ComputeContext>(ctx));

                    // Return a constant value so equality comparator always reports equal.
                    return k * 100;
                },
                // simple equality comparer
                (a, b) => a == b
            );

            var ctx = new ComputeContext("eq-c1");
            int v = cache.Get(ctx, 1);
            Assert.AreEqual(100, v);

            // Invalidate the generation context that produced the value.
            invalidators[1][0].TryGetTarget(out var genCtx);
            genCtx?.Invalidate();
            ComputeContext.FlushInvalidates();

            // Because the comparator says the new value is equal, the observer context should NOT have been invalidated.
            Assert.IsFalse(ctx.IsInvalidated);

            // The cached value should remain the same.
            v = cache.Get(ctx, 1);
            Assert.AreEqual(100, v);
        }

        [Test]
        public void TestEqualityComparator_InvalidateOnDifferent()
        {
            // With an equality comparator present, if the recomputed value differs, entries should be removed
            // and observers invalidated (same behavior as without a comparator).
            int seq = 0;
            Dictionary<int, List<WeakReference<ComputeContext>>> invalidators = new();

            PropCache<int, int> cache = new PropCache<int, int>(
                "eqDiffTest",
                (ctx, k) =>
                {
                    if (!invalidators.TryGetValue(k, out var list))
                    {
                        list = new List<WeakReference<ComputeContext>>();
                        invalidators[k] = list;
                    }

                    list.Add(new WeakReference<ComputeContext>(ctx));

                    return (k * 10) + seq++;
                },
                (a, b) => a == b
            );

            ComputeContext ctx = new ComputeContext("c1");
            int val = cache.Get(ctx, 1); // 10
            Assert.AreEqual(10, val);

            ComputeContext ctx2 = new ComputeContext("c2");
            val = cache.Get(ctx2, 1);    // 10
            Assert.AreEqual(10, val);

            ComputeContext ctx3 = new ComputeContext("c3");
            cache.Get(ctx3, 2);          // 21

            invalidators[1][0].TryGetTarget(out var target);
            target?.Invalidate();
            
            ComputeContext.FlushInvalidates();

            Assert.IsTrue(ctx.IsInvalidated);
            Assert.IsTrue(ctx2.IsInvalidated);
            Assert.IsFalse(ctx3.IsInvalidated);
            
            val = cache.Get(ctx, 1);
            Assert.AreEqual(13, val); // 13 currently as we compute on invalidate and again on get
        }

        [Test]
        public void TestInvalidateAllAndGlobalInvalidate()
        {
            int seq = 0;
            Dictionary<int, List<WeakReference<ComputeContext>>> invalidatorsA = new();
            Dictionary<int, List<WeakReference<ComputeContext>>> invalidatorsB = new();

            var cacheA = new PropCache<int, int>("cacheA", (ctx, k) =>
            {
                if (!invalidatorsA.TryGetValue(k, out var list))
                {
                    list = new List<WeakReference<ComputeContext>>();
                    invalidatorsA[k] = list;
                }

                list.Add(new WeakReference<ComputeContext>(ctx));
                return (k * 10) + seq++;
            });

            var cacheB = new PropCache<int, int>("cacheB", (ctx, k) =>
            {
                if (!invalidatorsB.TryGetValue(k, out var list))
                {
                    list = new List<WeakReference<ComputeContext>>();
                    invalidatorsB[k] = list;
                }

                list.Add(new WeakReference<ComputeContext>(ctx));
                return (k * 100) + seq++;
            });

            var ctxA = new ComputeContext("ctxA");
            var valA1 = cacheA.Get(ctxA, 1);

            var ctxB = new ComputeContext("ctxB");
            var valB1 = cacheB.Get(ctxB, 1);

            // Invalidate only cacheA via its instance API
            cacheA.InvalidateAll();
            ComputeContext.FlushInvalidates();

            Assert.IsTrue(ctxA.IsInvalidated, "cacheA observer should be invalidated after InvalidateAll");
            Assert.IsFalse(ctxB.IsInvalidated, "cacheB observer should remain valid");

            // Recreate observer for cacheA to repopulate
            var ctxA2 = new ComputeContext("ctxA2");
            var valA2 = cacheA.Get(ctxA2, 1);
            Assert.AreNotEqual(valA1, valA2, "cacheA should have produced a new value after being cleared");

            // Now invalidate all caches globally
            PropCacheDebug.InvalidateAllCaches();
            ComputeContext.FlushInvalidates();

            // Both cache observers should be invalidated
            Assert.IsTrue(ctxA2.IsInvalidated, "cacheA observer should be invalidated by global invalidate");
            Assert.IsTrue(ctxB.IsInvalidated, "cacheB observer should be invalidated by global invalidate");

            // New gets should produce new values
            var ctxA3 = new ComputeContext("ctxA3");
            var valA3 = cacheA.Get(ctxA3, 1);
            Assert.AreNotEqual(valA2, valA3);

            var ctxB2 = new ComputeContext("ctxB2");
            var valB2 = cacheB.Get(ctxB2, 1);
            Assert.AreNotEqual(valB1, valB2);
        }
    }
}