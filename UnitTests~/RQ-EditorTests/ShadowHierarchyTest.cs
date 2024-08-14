using System;
using System.Collections.Generic;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnitTests.EditorTests
{
    public class ShadowHierarchyTest
    {
        private List<UnityEngine.Object> createdObjects = new List<Object>();
        
        private T c<T>(T obj) where T: UnityEngine.Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        private GameObject c(string s)
        {
            return c(new GameObject(s));
        }
        
        [TearDown]
        public void TearDown()
        {
            foreach (var obj in createdObjects)
            {
                Object.DestroyImmediate(obj);
            }
            
            createdObjects.Clear();
        }
        
        [Test]
        public void TestBasic()
        {
            var shadow = new ShadowHierarchy();

            var gameObject = c(new GameObject("tmp"));

            var ctx = new ComputeContext("");
            bool wasFired = false;
            bool doInvalidate = false;
            
            shadow.RegisterGameObjectListener(gameObject, e =>
            {
                Assert.AreEqual(HierarchyEvent.ObjectDirty, e);
                wasFired = true;
                return doInvalidate;
            }, ctx);
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            Assert.IsTrue(wasFired);
            Assert.IsFalse(ctx.IsInvalidated);

            wasFired = false;
            doInvalidate = true;
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            Assert.IsTrue(wasFired);
            Assert.IsTrue(ctx.IsInvalidated);

            wasFired = false;
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            Assert.IsFalse(wasFired);
        }

        [Test]
        public void ListenerDeregisteredWhenContextInvalidated()
        {
            var shadow = new ShadowHierarchy();

            var gameObject = c(new GameObject("tmp"));

            var ctx = new ComputeContext("");
            bool wasFired = false;
            
            shadow.RegisterGameObjectListener(gameObject, e =>
            {
                wasFired = true;
                return true;
            }, ctx);
            
            ctx.Invalidate();
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            
            Assert.IsFalse(wasFired);
        }

        [Test]
        public void ListenerDeregisteredAfterTrueReturn()
        {
            var shadow = new ShadowHierarchy();
            var gameObject = c(new GameObject("tmp"));

            int count = 0;
            var ctx = new ComputeContext("");
            
            shadow.RegisterGameObjectListener(gameObject, (e) =>
            {
                count++;
                return true;
            }, ctx);
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            
            Assert.AreEqual(1, count);
        }
        
        void MakeListener__WhenTargetGCd_ListenerIsRemoved(ShadowHierarchy h, GameObject gameObject, bool[] wasFired)
        {
            h.RegisterGameObjectListener(gameObject, e =>
            {
                wasFired[0] = true;
                return false;
            }, new ComputeContext(""));
        }
        [Test]
        public void WhenTargetGCd_ListenerIsRemoved()
        {
            var shadow = new ShadowHierarchy();

            var gameObject = c(new GameObject("tmp"));

            bool[] wasFired = {false};
            
            // Ensure we don't have extra references on the stack still by creating the target object in a separate
            // stack frame
            MakeListener__WhenTargetGCd_ListenerIsRemoved(shadow, gameObject, wasFired);

            System.GC.Collect(999, GCCollectionMode.Forced, true);
            System.GC.WaitForPendingFinalizers();
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            
            Assert.IsFalse(wasFired[0]);
        }
        
        [Test]
        public void WhenDisposed_ListenerIsRemoved()
        {
            var shadow = new ShadowHierarchy();

            var gameObject = c(new GameObject("tmp"));

            var ctx = new ComputeContext("");
            bool wasFired = false;
            
            var listener = shadow.RegisterGameObjectListener(gameObject, e =>
            {
                wasFired = true;
                return false;
            }, ctx);

            listener.Dispose();
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            
            Assert.IsFalse(wasFired);
        }

        [Test]
        public void PathNotifications_GeneratedWhenImmediateParentChanged()
        {
            var shadow = new ShadowHierarchy();
            var p1 = c("p1");
            var p2 = c("p2");
            
            var target = new ComputeContext("");
            bool wasFired = false;
            
            shadow.RegisterGameObjectListener(p2, e =>
            {
                if (e == HierarchyEvent.PathChange)
                {
                    wasFired = true;
                }

                return false;
            }, target);
            shadow.EnablePathMonitoring(p2);
            
            p2.transform.SetParent(p1.transform);
            
            shadow.FireReparentNotification(p2.GetInstanceID());
            
            Assert.IsTrue(wasFired);
        }
        
        [Test]
        public void PathNotifications_GeneratedWhenGrandparentChanged()
        {
            var shadow = new ShadowHierarchy();
            var p1 = c("p1");
            var p2 = c("p2");
            var p3 = c("p3");
            
            var target = new ComputeContext("");
            bool wasFired = false;
            
            p1.transform.SetParent(p2.transform);
            
            shadow.RegisterGameObjectListener(p1, e =>
            {
                if (e == HierarchyEvent.PathChange)
                {
                    wasFired = true;
                }

                return false;
            }, target);
            shadow.EnablePathMonitoring(p1);
            
            p2.transform.SetParent(p3.transform);
            
            shadow.FireReparentNotification(p2.GetInstanceID());
            
            Assert.IsTrue(wasFired);
        }

        [Test]
        public void ComponentChangeNotifications_GeneratedWhenObjectItselfChanges()
        {
            var shadow = new ShadowHierarchy();
            var obj = c("obj");

            ComputeContext ctx = new ComputeContext("");
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(obj, e =>
            {
                events.Add(e);
                return false;
            }, ctx);
            
            shadow.FireStructureChangeEvent(obj.GetInstanceID());
            
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(HierarchyEvent.SelfComponentsChanged, events[0]);
        }
        
        [Test]
        public void ComponentChangeNotifications_GeneratedWhenChildChanges()
        {
            var shadow = new ShadowHierarchy();
            var parent = c("p");
            var child = c("c");
            
            child.transform.SetParent(parent.transform);
            
            ComputeContext ctx = new ComputeContext("");
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(parent, e =>
            {
                events.Add(e);
                return false;
            }, ctx);
            
            shadow.EnableComponentMonitoring(parent);
            shadow.FireStructureChangeEvent(child.GetInstanceID());
            
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(HierarchyEvent.ChildComponentsChanged, events[0]);
        }

        [Test]
        public void ComponentChangeNotifications_FiredAfterReparents()
        {
            var shadow = new ShadowHierarchy();
            var p1 = c("p1");
            var p2 = c("p2");
            var p3 = c("p3");
            
            p3.transform.SetParent(p2.transform);
            
            ComputeContext ctx = new ComputeContext("");
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(p1, e =>
            {
                events.Add(e);
                return false;
            }, ctx);
            
            shadow.EnableComponentMonitoring(p1);
            
            p2.transform.SetParent(p1.transform);
            shadow.FireReparentNotification(p2.GetInstanceID());
            
            // Assert.AreEqual(1, events.Count); - TODO - deduplicate events
            Assert.IsFalse(events.Contains(HierarchyEvent.PathChange)); // we didn't register for this
            Assert.IsTrue(events.Contains(HierarchyEvent.ChildComponentsChanged));
            
            events.Clear();
            
            shadow.FireStructureChangeEvent(p3.GetInstanceID());
            
            // Assert.AreEqual(1, events.Count); - TODO - deduplicate events
            Assert.IsTrue(events.Contains(HierarchyEvent.ChildComponentsChanged));
        }

        [Test]
        public void ComponentChangeNotification_FiredAfterReorderEvent()
        {
            var shadow = new ShadowHierarchy();
            var p = c("p");
            var c1 = c("c1");
            
            c1.transform.SetParent(p.transform);
            
            ComputeContext ctx = new ComputeContext("");
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(p, e =>
            {
                events.Add(e);
                return false;
            }, ctx);
            
            shadow.EnableComponentMonitoring(p);
            
            shadow.FireReorderNotification(c1.GetInstanceID());
            
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events.Contains(HierarchyEvent.ChildComponentsChanged));
        }

        [Test]
        public void OnDestroy_NotificationsBlasted()
        {
            var shadow = new ShadowHierarchy();
            
            var o1 = c("o1");
            var o2 = c("o2");
            var o3 = c("o3");
            
            o2.transform.SetParent(o1.transform);
            o3.transform.SetParent(o2.transform);
            
            ComputeContext ctx = new ComputeContext("");
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, e =>
            {
                events.Add((1, e));
                return false;
            }, ctx);
            shadow.RegisterGameObjectListener(o2, e =>
            {
                events.Add((2, e));
                return false;
            }, ctx);
            shadow.RegisterGameObjectListener(o3, e =>
            {
                events.Add((3, e));
                return false;
            }, ctx);
            
            shadow.EnableComponentMonitoring(o1);
            
            var o2_id = o2.GetInstanceID();
            Object.DestroyImmediate(o2);
            
            shadow.FireDestroyNotification(o2_id);
            
            Assert.Contains((1, HierarchyEvent.ChildComponentsChanged), events);
            Assert.Contains((2, HierarchyEvent.ForceInvalidate), events);
            Assert.Contains((3, HierarchyEvent.ForceInvalidate), events);
        }
        
        private ListenerSet<HierarchyEvent>.Filter AddToList(List<(int, HierarchyEvent)> events, int o)
        {
            return e =>
            {
                events.Add((o, e));
                return false;
            };
        }

        [Test]
        public void OnReparentDestroyedObject_NotificationsBlasted()
        {
            var shadow = new ShadowHierarchy();
            
            var ctx = new ComputeContext("");
            
            var o1 = c("o1");
            var o2 = c("o2");
            var o3 = c("o3");
            
            o2.transform.SetParent(o1.transform);
            o3.transform.SetParent(o2.transform);
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, AddToList(events, 1), ctx);
            shadow.RegisterGameObjectListener(o2, AddToList(events, 2), ctx);
            shadow.RegisterGameObjectListener(o3, AddToList(events, 3), ctx);
            
            shadow.EnableComponentMonitoring(o1);
            
            var o2_id = o2.GetInstanceID();
            Object.DestroyImmediate(o2);
            
            shadow.FireReparentNotification(o2_id);
            
            Assert.Contains((1, HierarchyEvent.ChildComponentsChanged), events);
            Assert.Contains((2, HierarchyEvent.ForceInvalidate), events);
            Assert.Contains((3, HierarchyEvent.ForceInvalidate), events);
        }

        [Test]
        public void OnInvalidateAll_EverythingIsInvalidated()
        {
            var shadow = new ShadowHierarchy();
                        
            var ctx1 = new ComputeContext("");
            var ctx2 = new ComputeContext("");
            var ctx3 = new ComputeContext("");
            
            var o1 = c("o1");
            var o2 = c("o2");
            var o3 = c("o3");
            
            o2.transform.SetParent(o1.transform);
            o3.transform.SetParent(o2.transform);
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, AddToList(events, 1), ctx1);
            shadow.RegisterGameObjectListener(o2, AddToList(events, 2), ctx2);
            shadow.RegisterGameObjectListener(o3, AddToList(events, 3), ctx3);
            
            shadow.InvalidateAll();
            shadow.FireObjectChangeNotification(o1.GetInstanceID()); // should be ignored
            
            Assert.IsTrue(ctx1.IsInvalidated);
            Assert.IsTrue(ctx2.IsInvalidated);
            Assert.IsTrue(ctx3.IsInvalidated);
        }

        [Test]
        public void ComponentMonitoringTest()
        {
            var shadow = new ShadowHierarchy();
            
            var ctx = new ComputeContext("");
            
            var o1 = c("o1");
            var component = o1.AddComponent<Camera>();
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterObjectListener(component, AddToList(events, 1), ctx);
            
            shadow.FireObjectChangeNotification(component.GetInstanceID());
            
            Assert.Contains((1, HierarchyEvent.ObjectDirty), events);
        }

        [Test]
        public void ComponentReorder_TriggersStructureChange()
        {
            var shadow = new ShadowHierarchy();
            
            var ctx = new ComputeContext("");

            var o1 = c("o1");
            var o2 = c("o2");

            var c1 = o2.AddComponent<BoxCollider>();
            var c2 = o2.AddComponent<BoxCollider>();
            
            var (iid1, iid2) = (c1.GetInstanceID(), c2.GetInstanceID());
            
            o2.transform.SetParent(o1.transform);
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, AddToList(events, 1), ctx);
            shadow.RegisterGameObjectListener(o2, AddToList(events, 2), ctx);
            
            shadow.EnableComponentMonitoring(o1);

            UnityEditorInternal.ComponentUtility.MoveComponentUp(c2);
            var newComponents = o2.GetComponents<BoxCollider>();
            var (iid1a, iid2a) = (newComponents[0].GetInstanceID(), newComponents[1].GetInstanceID());
            
            Assert.AreEqual(iid1, iid2a);
            Assert.AreEqual(iid2, iid1a);
            
            shadow.FireObjectChangeNotification(iid1);
            shadow.FireObjectChangeNotification(iid2);
            
            Assert.Contains((1, HierarchyEvent.ChildComponentsChanged), events);
            Assert.Contains((2, HierarchyEvent.SelfComponentsChanged), events);
        }
        
        // TODO - hierarchy pruning
        // TODO - test create game object notifications
        // TODO - test asset objects
    }
}