using System;
using System.Collections.Generic;
using nadena.dev.ndmf.rq.unity.editor;
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

            var target = new object();
            bool wasFired = false;
            
            shadow.RegisterGameObjectListener(gameObject, (o, e) =>
            {
                Assert.AreEqual(target, o);
                Assert.AreEqual(HierarchyEvent.ObjectDirty, e);
                wasFired = true;
                return false;
            }, target);
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            Assert.IsTrue(wasFired);

            wasFired = false;
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            Assert.IsTrue(wasFired);
        }

        [Test]
        public void ListenerDeregisteredAfterTrueReturn()
        {
            var shadow = new ShadowHierarchy();
            var gameObject = c(new GameObject("tmp"));

            int count = 0;
            var target = new object();
            
            shadow.RegisterGameObjectListener(gameObject, (o, e) =>
            {
                count++;
                return true;
            }, target);
            
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            shadow.FireObjectChangeNotification(gameObject.GetInstanceID());
            
            Assert.AreEqual(1, count);
        }
        
        void MakeListener__WhenTargetGCd_ListenerIsRemoved(ShadowHierarchy h, GameObject gameObject, bool[] wasFired)
        {
            h.RegisterGameObjectListener(gameObject, (o,e ) =>
            {
                wasFired[0] = true;
                return false;
            }, new object());
        }
        [Test]
        public void WhenTargetGCd_ListenerIsRemoved()
        {
            var shadow = new ShadowHierarchy();

            var gameObject = c(new GameObject("tmp"));

            var target = new object();
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

            var target = new object();
            bool wasFired = false;
            
            var listener = shadow.RegisterGameObjectListener(gameObject, (o, e) =>
            {
                Assert.AreEqual(target, e);
                Assert.AreEqual(HierarchyEvent.ObjectDirty, o);
                wasFired = true;
                return false;
            }, target);

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
            
            var target = new object();
            bool wasFired = false;
            
            shadow.RegisterGameObjectListener(p2, (o, e) =>
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
            
            var target = new object();
            bool wasFired = false;
            
            p1.transform.SetParent(p2.transform);
            
            shadow.RegisterGameObjectListener(p1, (o, e) =>
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
            
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(obj, (o, e) =>
            {
                events.Add(e);
                return false;
            }, events);
            
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
            
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(parent, (o, e) =>
            {
                events.Add(e);
                return false;
            }, events);
            
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
            
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(p1, (o, e) =>
            {
                events.Add(e);
                return false;
            }, events);
            
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
            
            List<HierarchyEvent> events = new List<HierarchyEvent>();
            
            shadow.RegisterGameObjectListener(p, (o, e) =>
            {
                events.Add(e);
                return false;
            }, events);
            
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
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, (o, e) =>
            {
                events.Add(((int) o, e));
                return false;
            }, 1);
            shadow.RegisterGameObjectListener(o2, (o, e) =>
            {
                events.Add(((int) o, e));
                return false;
            }, 2);
            shadow.RegisterGameObjectListener(o3, (o, e) =>
            {
                events.Add(((int) o, e));
                return false;
            }, 3);
            
            shadow.EnableComponentMonitoring(o1);
            
            var o2_id = o2.GetInstanceID();
            Object.DestroyImmediate(o2);
            
            shadow.FireDestroyNotification(o2_id);
            
            Assert.Contains((1, HierarchyEvent.ChildComponentsChanged), events);
            Assert.Contains((2, HierarchyEvent.ForceInvalidate), events);
            Assert.Contains((3, HierarchyEvent.ForceInvalidate), events);
        }
        
        private ListenerSet<HierarchyEvent>.Invokee AddToList(List<(int, HierarchyEvent)> events)
        {
            return (o, e) =>
            {
                events.Add(((int) o, e));
                return false;
            };
        }

        [Test]
        public void OnReparentDestroyedObject_NotificationsBlasted()
        {
            var shadow = new ShadowHierarchy();
            
            var o1 = c("o1");
            var o2 = c("o2");
            var o3 = c("o3");
            
            o2.transform.SetParent(o1.transform);
            o3.transform.SetParent(o2.transform);
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, AddToList(events), 1);
            shadow.RegisterGameObjectListener(o2, AddToList(events), 2);
            shadow.RegisterGameObjectListener(o3, AddToList(events), 3);
            
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
            
            var o1 = c("o1");
            var o2 = c("o2");
            var o3 = c("o3");
            
            o2.transform.SetParent(o1.transform);
            o3.transform.SetParent(o2.transform);
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, AddToList(events), 1);
            shadow.RegisterGameObjectListener(o2, AddToList(events), 2);
            shadow.RegisterGameObjectListener(o3, AddToList(events), 3);
            
            shadow.InvalidateAll();
            shadow.FireObjectChangeNotification(o1.GetInstanceID()); // should be ignored
            
            Assert.Contains((1, HierarchyEvent.ForceInvalidate), events);
            Assert.Contains((2, HierarchyEvent.ForceInvalidate), events);
            Assert.Contains((3, HierarchyEvent.ForceInvalidate), events);
            Assert.IsFalse(events.Contains((1, HierarchyEvent.ObjectDirty)));
        }

        [Test]
        public void ComponentMonitoringTest()
        {
            var shadow = new ShadowHierarchy();
            
            var o1 = c("o1");
            var component = o1.AddComponent<Camera>();
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterObjectListener(component, AddToList(events), 1);
            
            shadow.FireObjectChangeNotification(component.GetInstanceID());
            
            Assert.Contains((1, HierarchyEvent.ObjectDirty), events);
        }

        [Test]
        public void ComponentReorder_TriggersStructureChange()
        {
            var shadow = new ShadowHierarchy();
            
            var o1 = c("o1");
            var o2 = c("o2");

            var c1 = o2.AddComponent<BoxCollider>();
            var c2 = o2.AddComponent<BoxCollider>();
            
            var (iid1, iid2) = (c1.GetInstanceID(), c2.GetInstanceID());
            
            o2.transform.SetParent(o1.transform);
            
            List<(int, HierarchyEvent)> events = new List<(int, HierarchyEvent)>();
            shadow.RegisterGameObjectListener(o1, AddToList(events), 1);
            shadow.RegisterGameObjectListener(o2, AddToList(events), 2);
            
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