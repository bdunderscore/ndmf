using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnitTests
{
    /// <summary>
    ///     Covers ComputeContextQueries.ObserveTransformPosition, which routes through a shared PropCache so that
    ///     the (relatively expensive) position comparison is evaluated once per transform rather than once per
    ///     downstream observer. The cache must not change the observable contract: every observer of a transform
    ///     still has to be invalidated when that transform's world position changes, and must not be invalidated
    ///     when it doesn't.
    /// </summary>
    public class ObserveTransformPositionTest : TestBase
    {
        /// <summary>
        ///     Simulates the change stream event the editor would emit after a transform's properties are touched.
        /// </summary>
        private static void FireChanged(Transform t)
        {
            ObjectWatcher.Instance.Hierarchy.FireObjectChangeNotification(t.GetInstanceID());
            ComputeContext.FlushInvalidates();
        }

        private static void FireReparented(GameObject go)
        {
            ObjectWatcher.Instance.Hierarchy.FireReparentNotification(go.GetInstanceID());
            ComputeContext.FlushInvalidates();
        }

        private GameObject NewObject(string name)
        {
            return TrackObject(new GameObject(name));
        }

        [Test]
        public void WhenTransformMoves_ObserverIsInvalidated()
        {
            var go = NewObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            Assert.IsFalse(ctx.IsInvalidated);

            go.transform.localPosition = new Vector3(1, 0, 0);
            FireChanged(go.transform);

            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void WhenTransformRotatesOrScales_ObserverIsInvalidated()
        {
            var rotated = NewObject("rotated");
            var scaled = NewObject("scaled");

            var rotateCtx = new ComputeContext("rotate-observer");
            rotateCtx.ObserveTransformPosition(rotated.transform);

            var scaleCtx = new ComputeContext("scale-observer");
            scaleCtx.ObserveTransformPosition(scaled.transform);

            rotated.transform.localRotation = Quaternion.Euler(0, 90, 0);
            FireChanged(rotated.transform);

            scaled.transform.localScale = new Vector3(2, 2, 2);
            FireChanged(scaled.transform);

            Assert.IsTrue(rotateCtx.IsInvalidated);
            Assert.IsTrue(scaleCtx.IsInvalidated);
        }

        [Test]
        public void WhenTransformDoesNotMove_ObserverIsNotInvalidated()
        {
            var go = NewObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            // A change event with no actual movement; the epsilon comparison should absorb it.
            FireChanged(go.transform);

            Assert.IsFalse(ctx.IsInvalidated);
        }

        [Test]
        public void WhenTransformMovesBelowEpsilon_ObserverIsNotInvalidated()
        {
            var go = NewObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            // Threshold is 0.0001; this is an order of magnitude below it.
            go.transform.localPosition = new Vector3(0.00001f, 0, 0);
            FireChanged(go.transform);

            Assert.IsFalse(ctx.IsInvalidated);
        }

        [Test]
        public void WhenTransformMoves_AllSharedObserversAreInvalidated()
        {
            // The dedup case: the second and third Get() calls are cache hits, and must still be wired up to the
            // shared cache entry's invalidation.
            var go = NewObject("target");

            var ctx1 = new ComputeContext("observer-1");
            var ctx2 = new ComputeContext("observer-2");
            var ctx3 = new ComputeContext("observer-3");

            ctx1.ObserveTransformPosition(go.transform);
            ctx2.ObserveTransformPosition(go.transform);
            ctx3.ObserveTransformPosition(go.transform);

            go.transform.localPosition = new Vector3(1, 0, 0);
            FireChanged(go.transform);

            Assert.IsTrue(ctx1.IsInvalidated);
            Assert.IsTrue(ctx2.IsInvalidated);
            Assert.IsTrue(ctx3.IsInvalidated);
        }

        [Test]
        public void AfterInvalidation_NewObserversAreStillInvalidated()
        {
            // Once a cache entry has been invalidated and evicted, observing again must produce a fresh, live entry
            // rather than a stale one that never fires.
            var go = NewObject("target");

            var ctx1 = new ComputeContext("observer-1");
            ctx1.ObserveTransformPosition(go.transform);

            go.transform.localPosition = new Vector3(1, 0, 0);
            FireChanged(go.transform);

            Assert.IsTrue(ctx1.IsInvalidated);

            var ctx2 = new ComputeContext("observer-2");
            ctx2.ObserveTransformPosition(go.transform);

            Assert.IsFalse(ctx2.IsInvalidated);

            go.transform.localPosition = new Vector3(2, 0, 0);
            FireChanged(go.transform);

            Assert.IsTrue(ctx2.IsInvalidated);
        }

        [Test]
        public void WhenAncestorMoves_ObserverIsInvalidated()
        {
            // World position depends on the whole path, so a parent's movement has to invalidate observers of
            // the child even though the child's own local transform is untouched.
            var parent = NewObject("parent");
            var child = NewObject("child");
            child.transform.SetParent(parent.transform, false);

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(child.transform);

            parent.transform.localPosition = new Vector3(1, 0, 0);
            FireChanged(parent.transform);

            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void WhenUnrelatedTransformMoves_ObserverIsNotInvalidated()
        {
            // Verifies the cache is keyed per transform, and that observers aren't cross-wired.
            var a = NewObject("a");
            var b = NewObject("b");

            var ctxA = new ComputeContext("observer-a");
            ctxA.ObserveTransformPosition(a.transform);

            var ctxB = new ComputeContext("observer-b");
            ctxB.ObserveTransformPosition(b.transform);

            b.transform.localPosition = new Vector3(1, 0, 0);
            FireChanged(b.transform);

            Assert.IsFalse(ctxA.IsInvalidated);
            Assert.IsTrue(ctxB.IsInvalidated);
        }

        [Test]
        public void WhenReparented_ObserverIsInvalidated()
        {
            var oldParent = NewObject("old-parent");
            var newParent = NewObject("new-parent");
            var child = NewObject("child");

            child.transform.SetParent(oldParent.transform, false);
            newParent.transform.localPosition = new Vector3(5, 0, 0);

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(child.transform);

            Assert.IsFalse(ctx.IsInvalidated);

            child.transform.SetParent(newParent.transform, false);
            FireReparented(child);

            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void WhenObservedTransformIsDestroyed_ObserverIsInvalidatedWithoutThrowing()
        {
            // The cache key is a Transform, so eviction has to cope with the key having been destroyed out from
            // under it rather than trying to re-run the comparison against a dead object.
            // Not TrackObject'd: this test destroys the object itself, and teardown must not destroy it twice.
            var go = new GameObject("target");
            var instanceId = go.GetInstanceID();

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            Object.DestroyImmediate(go);

            Assert.DoesNotThrow(() =>
            {
                ObjectWatcher.Instance.Hierarchy.FireDestroyNotification(instanceId);
                ComputeContext.FlushInvalidates();
            });

            Assert.IsTrue(ctx.IsInvalidated);
        }
    }
}
