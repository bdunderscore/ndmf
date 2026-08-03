using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnitTests
{
    /// <summary>
    ///     Covers ComputeContextQueries.ObserveTransformPosition, which uses one Burst job to check every monitored
    ///     transform each editor frame. The job result is consumed on the following frame.
    /// </summary>
    public class ObserveTransformPositionTest : TestBase
    {
        private static void AdvanceFrame()
        {
            TransformPositionMonitor.UpdateForTesting();
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

            go.transform.position = new Vector3(1, 0, 0);
            AdvanceFrame(); // Schedule the Burst read.
            AdvanceFrame(); // Consume the result on the following frame.

            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void WhenTransformMovesBeforePendingMonitorIsRegistered_ObserverIsInvalidated()
        {
            var go = NewObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            // The monitor has only been queued at this point. Moving before the first update must be compared
            // against the matrix captured by ObserveTransformPosition, not a new matrix captured during Update.
            go.transform.position = new Vector3(1, 0, 0);
            AdvanceFrame();

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
            scaled.transform.localScale = new Vector3(2, 2, 2);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsTrue(rotateCtx.IsInvalidated);
            Assert.IsTrue(scaleCtx.IsInvalidated);
        }

        [Test]
        public void WhenTransformDoesNotMove_ObserverIsNotInvalidated()
        {
            var go = NewObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            AdvanceFrame();
            AdvanceFrame();

            Assert.IsFalse(ctx.IsInvalidated);
        }

        [Test]
        public void WhenTransformMovesBelowEpsilon_ObserverIsNotInvalidated()
        {
            var go = NewObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            // Threshold is 0.0001; this is an order of magnitude below it.
            go.transform.position = new Vector3(0.00001f, 0, 0);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsFalse(ctx.IsInvalidated);
        }

        [Test]
        public void WhenTransformMoves_AllSharedObserversAreInvalidated()
        {
            var go = NewObject("target");

            var ctx1 = new ComputeContext("observer-1");
            var ctx2 = new ComputeContext("observer-2");
            var ctx3 = new ComputeContext("observer-3");

            ctx1.ObserveTransformPosition(go.transform);
            ctx2.ObserveTransformPosition(go.transform);
            ctx3.ObserveTransformPosition(go.transform);

            go.transform.position = new Vector3(1, 0, 0);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsTrue(ctx1.IsInvalidated);
            Assert.IsTrue(ctx2.IsInvalidated);
            Assert.IsTrue(ctx3.IsInvalidated);
        }

        [Test]
        public void AfterInvalidation_NewObserversAreStillInvalidated()
        {
            // Once a monitor has fired and released its slot, observing again must produce a fresh, live monitor.
            var go = NewObject("target");

            var ctx1 = new ComputeContext("observer-1");
            ctx1.ObserveTransformPosition(go.transform);

            go.transform.position = new Vector3(1, 0, 0);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsTrue(ctx1.IsInvalidated);

            var ctx2 = new ComputeContext("observer-2");
            ctx2.ObserveTransformPosition(go.transform);

            Assert.IsFalse(ctx2.IsInvalidated);

            go.transform.position = new Vector3(2, 0, 0);
            AdvanceFrame();
            AdvanceFrame();

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

            parent.transform.position = new Vector3(1, 0, 0);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsTrue(ctx.IsInvalidated);
        }

        [Test]
        public void WhenUnrelatedTransformMoves_ObserverIsNotInvalidated()
        {
            // Verifies monitors are keyed per transform, and that observers aren't cross-wired.
            var a = NewObject("a");
            var b = NewObject("b");

            var ctxA = new ComputeContext("observer-a");
            ctxA.ObserveTransformPosition(a.transform);

            var ctxB = new ComputeContext("observer-b");
            ctxB.ObserveTransformPosition(b.transform);

            b.transform.position = new Vector3(1, 0, 0);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsFalse(ctxA.IsInvalidated);
            Assert.IsTrue(ctxB.IsInvalidated);
        }

        [Test]
        public void WhenReparentedWithoutMoving_ObserverIsNotInvalidated()
        {
            var oldParent = NewObject("old-parent");
            var newParent = NewObject("new-parent");
            var child = NewObject("child");

            child.transform.SetParent(oldParent.transform, true);
            newParent.transform.position = new Vector3(5, 0, 0);

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(child.transform);

            Assert.IsFalse(ctx.IsInvalidated);

            child.transform.SetParent(newParent.transform, true);
            AdvanceFrame();
            AdvanceFrame();

            Assert.IsFalse(ctx.IsInvalidated);
        }

        [Test]
        public void WhenObservedTransformIsDestroyed_ObserverIsInvalidatedWithoutThrowing()
        {
            // Not TrackObject'd: this test destroys the object itself, and teardown must not destroy it twice.
            var go = new GameObject("target");

            var ctx = new ComputeContext("observer");
            ctx.ObserveTransformPosition(go.transform);

            Object.DestroyImmediate(go);

            Assert.DoesNotThrow(() =>
            {
                AdvanceFrame();
            });

            Assert.IsTrue(ctx.IsInvalidated);
        }
    }
}
