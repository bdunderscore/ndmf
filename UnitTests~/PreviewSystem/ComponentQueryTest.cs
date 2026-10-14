using System;
using System.Collections;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UnitTests
{
    public class ComponentQueryTest
    {
        public enum QueryKind
        {
            Single,
            Multiple,
            Children,
        }

        private readonly List<GameObject> _objects = new();
        private readonly List<ComputeContext> _contexts = new();
        private GameObject _root;
        private bool _propertyMonitorWasEnabled;

        [SetUp]
        public void SetUp()
        {
            _propertyMonitorWasEnabled = ObjectWatcher.Instance.PropertyMonitor.IsEnabled;
            ObjectWatcher.Instance.PropertyMonitor.IsEnabled = false;
            _root = NewObject("query-root");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ctx in _contexts) ctx.Invalidate();
            ComputeContext.FlushInvalidates();
            foreach (var obj in _objects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _contexts.Clear();
            _objects.Clear();
            ObjectWatcher.Instance.PropertyMonitor.IsEnabled = _propertyMonitorWasEnabled;
        }

        private GameObject NewObject(string name, GameObject parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null) obj.transform.SetParent(parent.transform, false);
            _objects.Add(obj);
            return obj;
        }

        private ComputeContext NewContext()
        {
            var ctx = new ComputeContext("component-query-test");
            _contexts.Add(ctx);
            return ctx;
        }

        private static Component[] Query(ComputeContext ctx, GameObject root, QueryKind kind)
        {
            switch (kind)
            {
                case QueryKind.Single:
                    return new Component[] { ctx.GetComponent<BoxCollider>(root) };
                case QueryKind.Multiple:
                    return ctx.GetComponents<BoxCollider>(root);
                case QueryKind.Children:
                    return ctx.GetComponentsInChildren<BoxCollider>(root, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void NotifyStructure(GameObject obj)
        {
            ObjectWatcher.Instance.Hierarchy.MaybeFireStructureChangeEvent(obj.GetEntityId());
            ComputeContext.FlushInvalidates();
        }

        [TestCase(QueryKind.Single, false)]
        [TestCase(QueryKind.Single, true)]
        [TestCase(QueryKind.Multiple, false)]
        [TestCase(QueryKind.Multiple, true)]
        [TestCase(QueryKind.Children, false)]
        [TestCase(QueryKind.Children, true)]
        public void UnrelatedComponentChangesDoNotInvalidate(QueryKind kind, bool hasMatch)
        {
            var target = kind == QueryKind.Children ? NewObject("child", _root) : _root;
            if (hasMatch) target.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);
            Assert.That(ctx.IsInvalidated, Is.False);

            var unrelated = target.AddComponent<SphereCollider>();
            NotifyStructure(target);
            Assert.That(ctx.IsInvalidated, Is.False);

            Object.DestroyImmediate(unrelated);
            NotifyStructure(target);
            Assert.That(ctx.IsInvalidated, Is.False);
        }

        [TestCase(QueryKind.Single)]
        [TestCase(QueryKind.Multiple)]
        [TestCase(QueryKind.Children)]
        public void MatchingComponentAdditionAndDeletionInvalidateFreshObservers(QueryKind kind)
        {
            var target = kind == QueryKind.Children ? NewObject("child", _root) : _root;
            var beforeAddition = NewContext();
            Query(beforeAddition, _root, kind);

            var matching = target.AddComponent<BoxCollider>();
            NotifyStructure(target);
            Assert.That(beforeAddition.IsInvalidated, Is.True);

            var beforeDeletion = NewContext();
            Assert.That(Query(beforeDeletion, _root, kind), Is.EqualTo(new[] { matching }));
            Assert.That(beforeDeletion.IsInvalidated, Is.False);
            Object.DestroyImmediate(matching);
            NotifyStructure(target);
            Assert.That(beforeDeletion.IsInvalidated, Is.True);
        }

        [Test]
        public void AddingAnotherMatchDoesNotInvalidateSingleComponentQuery()
        {
            var first = _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Assert.That(ctx.GetComponent<BoxCollider>(_root), Is.SameAs(first));

            _root.AddComponent<BoxCollider>();
            NotifyStructure(_root);

            Assert.That(ctx.IsInvalidated, Is.False);
            Assert.That(_root.GetComponent<BoxCollider>(), Is.SameAs(first));
        }

        [TestCase(QueryKind.Multiple)]
        [TestCase(QueryKind.Children)]
        public void MatchingComponentOrderChangesInvalidate(QueryKind kind)
        {
            var target = kind == QueryKind.Children ? NewObject("child", _root) : _root;
            var first = target.AddComponent<BoxCollider>();
            var second = target.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Assert.That(Query(ctx, _root, kind), Is.EqualTo(new[] { first, second }));

            Assert.That(ComponentUtility.MoveComponentUp(second), Is.True);
            NotifyStructure(target);
            Assert.That(ctx.IsInvalidated, Is.True);
            Assert.That(Query(NewContext(), _root, kind), Is.EqualTo(new[] { second, first }));
        }

        [TestCase(QueryKind.Single)]
        [TestCase(QueryKind.Multiple)]
        [TestCase(QueryKind.Children)]
        public void ForcedInvalidationIsNotSuppressedByUnchangedResults(QueryKind kind)
        {
            var ctx = NewContext();
            Query(ctx, _root, kind);
            ObjectWatcher.Instance.Hierarchy.InvalidateTree(_root.GetEntityId());
            ComputeContext.FlushInvalidates();
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [UnityTest]
        public IEnumerator PropertyMonitorInvalidatesOnlyChangedQueryResults()
        {
            // An isolated watcher prevents editor events from satisfying the polling assertions.
            var watcher = new ObjectWatcher();
            _root.AddComponent<BoxCollider>();
            var child = NewObject("child", _root);
            var single = NewContext();
            var absent = NewContext();
            var multiple = NewContext();
            var children = NewContext();
            watcher.MonitorComponent(_root, single, () => _root.GetComponent<BoxCollider>());
            watcher.MonitorComponent(_root, absent, () => _root.GetComponent<CapsuleCollider>());
            watcher.MonitorComponents(_root, multiple, false, () => _root.GetComponents<BoxCollider>());
            watcher.MonitorComponents(_root, children, true, () => _root.GetComponentsInChildren<BoxCollider>(true));

            watcher.PropertyMonitor.IsEnabled = true;
            try
            {
                _root.AddComponent<SphereCollider>();
                yield return RefreshProperties(watcher.PropertyMonitor);
                Assert.That(single.IsInvalidated, Is.False);
                Assert.That(absent.IsInvalidated, Is.False);
                Assert.That(multiple.IsInvalidated, Is.False);
                Assert.That(children.IsInvalidated, Is.False);

                child.AddComponent<BoxCollider>();
                yield return RefreshProperties(watcher.PropertyMonitor);
                Assert.That(single.IsInvalidated, Is.False);
                Assert.That(absent.IsInvalidated, Is.False);
                Assert.That(multiple.IsInvalidated, Is.False);
                Assert.That(children.IsInvalidated, Is.True);

                _root.AddComponent<CapsuleCollider>();
                yield return RefreshProperties(watcher.PropertyMonitor);
                Assert.That(single.IsInvalidated, Is.False);
                Assert.That(absent.IsInvalidated, Is.True);
                Assert.That(multiple.IsInvalidated, Is.False);
            }
            finally
            {
                watcher.PropertyMonitor.IsEnabled = false;
            }
        }

        private static IEnumerator RefreshProperties(PropertyMonitor monitor)
        {
            var refresh = monitor.CheckAllObjects();
            yield return new WaitUntil(() => refresh.IsCompleted);
            refresh.GetAwaiter().GetResult();
            ComputeContext.FlushInvalidates();
        }
    }
}
