using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditor;
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
            SingleGeneric,
            SingleType,
            ArrayGeneric,
            ArrayType,
            ListGeneric,
            ListType,
            ChildrenArrayGeneric,
            ChildrenArrayType,
            ChildrenListGeneric,
        }

        private static readonly QueryKind[] AllQueries = (QueryKind[])Enum.GetValues(typeof(QueryKind));
        private static readonly QueryKind[] MultipleQueries = AllQueries
            .Where(kind => kind != QueryKind.SingleGeneric && kind != QueryKind.SingleType).ToArray();
        private static readonly QueryKind[] ChildrenQueries = AllQueries.Where(IncludesChildren).ToArray();

        private readonly List<GameObject> _objects = new();
        private readonly List<ComputeContext> _contexts = new();
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = NewObject("query-root");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ctx in _contexts) ctx.Invalidate();
            ComputeContext.FlushInvalidates();
            foreach (var obj in _objects)
            {
                if (obj != null)
                {
                    Undo.ClearUndo(obj);
                    Object.DestroyImmediate(obj);
                }
            }
            _contexts.Clear();
            _objects.Clear();
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

        private static bool IncludesChildren(QueryKind kind)
        {
            return kind == QueryKind.ChildrenArrayGeneric || kind == QueryKind.ChildrenArrayType ||
                   kind == QueryKind.ChildrenListGeneric;
        }

        private static IList<Component> Query(ComputeContext ctx, GameObject root, QueryKind kind,
            bool includeInactive = true)
        {
            switch (kind)
            {
                case QueryKind.SingleGeneric:
                    return new Component[] { ctx.GetComponent<BoxCollider>(root) };
                case QueryKind.SingleType:
                    return new[] { ctx.GetComponent(root, typeof(BoxCollider)) };
                case QueryKind.ArrayGeneric:
                    return ctx.GetComponents<BoxCollider>(root);
                case QueryKind.ArrayType:
                    return ctx.GetComponents(root, typeof(BoxCollider));
                case QueryKind.ListGeneric:
                {
                    var list = new List<BoxCollider> { null };
                    ctx.GetComponents(root, list);
                    return list.Cast<Component>().ToArray();
                }
                case QueryKind.ListType:
                {
                    var list = new List<Component> { null };
                    ctx.GetComponents(root, typeof(BoxCollider), list);
                    return list;
                }
                case QueryKind.ChildrenArrayGeneric:
                    return ctx.GetComponentsInChildren<BoxCollider>(root, includeInactive);
                case QueryKind.ChildrenArrayType:
                    return ctx.GetComponentsInChildren(root, typeof(BoxCollider), includeInactive);
                case QueryKind.ChildrenListGeneric:
                {
                    var list = new List<BoxCollider> { null };
                    ctx.GetComponentsInChildren(root, includeInactive, list);
                    return list.Cast<Component>().ToArray();
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void NotifyStructure(GameObject obj)
        {
            ObjectWatcher.Instance.Hierarchy.MaybeFireStructureChangeEvent(obj.GetEntityId());
            ComputeContext.FlushInvalidates();
        }

        private static void NotifyProperties(GameObject obj)
        {
            ObjectWatcher.Instance.Hierarchy.FireObjectChangeNotification(obj.GetEntityId());
            ComputeContext.FlushInvalidates();
        }

        private static void NotifyReparent(GameObject obj)
        {
            ObjectWatcher.Instance.Hierarchy.FireReparentNotification(obj.GetEntityId());
            ComputeContext.FlushInvalidates();
        }

        [TestCaseSource(nameof(AllQueries))]
        public void UnrelatedComponentChangesDoNotInvalidate(QueryKind kind)
        {
            var expected = _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Assert.That(Query(ctx, _root, kind), Is.EqualTo(new[] { expected }));

            var unrelated = _root.AddComponent<SphereCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.False);

            Object.DestroyImmediate(unrelated);
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.False);

            Object.DestroyImmediate(expected);
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [TestCaseSource(nameof(AllQueries))]
        public void MatchingComponentAdditionInvalidatesAnEmptyResult(QueryKind kind)
        {
            var ctx = NewContext();
            Query(ctx, _root, kind);

            var added = _root.AddComponent<BoxCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.True);
            Assert.That(Query(NewContext(), _root, kind), Is.EqualTo(new[] { added }));
        }

        [TestCaseSource(nameof(AllQueries))]
        public void ReplacingADestroyedComponentInvalidatesEvenWithTheSameResultCount(QueryKind kind)
        {
            var oldComponent = _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            Object.DestroyImmediate(oldComponent);
            var replacement = _root.AddComponent<BoxCollider>();
            NotifyStructure(_root);

            Assert.That(ctx.IsInvalidated, Is.True);
            Assert.That(Query(NewContext(), _root, kind), Is.EqualTo(new[] { replacement }));
        }

        [TestCaseSource(nameof(AllQueries))]
        public void ComponentOrderChangesInvalidate(QueryKind kind)
        {
            var first = _root.AddComponent<BoxCollider>();
            var second = _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            Assert.That(ComponentUtility.MoveComponentUp(second), Is.True);
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.True);

            var result = Query(NewContext(), _root, kind);
            Assert.That(result[0], Is.SameAs(second));
            if (kind != QueryKind.SingleGeneric && kind != QueryKind.SingleType)
            {
                Assert.That(result[1], Is.SameAs(first));
            }
        }

        [TestCase(QueryKind.SingleGeneric)]
        [TestCase(QueryKind.SingleType)]
        public void AddingAnotherMatchDoesNotInvalidateTheFirstComponent(QueryKind kind)
        {
            var first = _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            _root.AddComponent<BoxCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.False);
            Assert.That(Query(NewContext(), _root, kind)[0], Is.SameAs(first));
        }

        [TestCaseSource(nameof(AllQueries))]
        public void DescendantComponentChangesOnlyInvalidateChildQueries(QueryKind kind)
        {
            var child = NewObject("child", _root);
            var ctx = NewContext();
            Query(ctx, _root, kind);

            child.AddComponent<BoxCollider>();
            NotifyStructure(child);
            Assert.That(ctx.IsInvalidated, Is.EqualTo(IncludesChildren(kind)));
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void UnrelatedDescendantComponentsDoNotInvalidate(QueryKind kind)
        {
            var child = NewObject("child", _root);
            var ctx = NewContext();
            Query(ctx, _root, kind);

            child.AddComponent<SphereCollider>();
            NotifyStructure(child);
            Assert.That(ctx.IsInvalidated, Is.False);

            child.AddComponent<BoxCollider>();
            NotifyStructure(child);
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [TestCaseSource(nameof(MultipleQueries))]
        public void MutatingTheReturnedArrayDoesNotChangeTheObservedSnapshot(QueryKind kind)
        {
            _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            var result = Query(ctx, _root, kind);
            result[0] = null;

            _root.AddComponent<SphereCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.False);

            _root.AddComponent<BoxCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [Test]
        public void ReusingOutputListsDoesNotChangeObservedSnapshots()
        {
            _root.AddComponent<BoxCollider>();
            var other = NewObject("other");
            other.AddComponent<BoxCollider>();
            var generic = new List<BoxCollider>();
            var runtimeTyped = new List<Component>();
            var children = new List<BoxCollider>();
            var ctx = NewContext();
            ctx.GetComponents(_root, generic);
            ctx.GetComponents(_root, typeof(BoxCollider), runtimeTyped);
            ctx.GetComponentsInChildren(_root, true, children);

            var otherContext = NewContext();
            otherContext.GetComponents(other, generic);
            otherContext.GetComponents(other, typeof(BoxCollider), runtimeTyped);
            otherContext.GetComponentsInChildren(other, true, children);
            generic.Clear();
            runtimeTyped.Clear();
            children.Clear();

            _root.AddComponent<SphereCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.False);
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void SiblingOrderChangesInvalidateChildQueries(QueryKind kind)
        {
            var first = NewObject("first", _root);
            var second = NewObject("second", _root);
            first.AddComponent<BoxCollider>();
            var moved = second.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            second.transform.SetSiblingIndex(0);
            ObjectWatcher.Instance.Hierarchy.FireReorderNotification(_root.GetEntityId());
            ComputeContext.FlushInvalidates();

            Assert.That(ctx.IsInvalidated, Is.True);
            Assert.That(Query(NewContext(), _root, kind)[0], Is.SameAs(moved));
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void ReparentingOnlyInvalidatesWhenTheResultChanges(QueryKind kind)
        {
            var empty = NewObject("empty", _root);
            var other = NewObject("other");
            var ctx = NewContext();
            Query(ctx, _root, kind);

            empty.transform.SetParent(other.transform, false);
            NotifyReparent(empty);
            Assert.That(ctx.IsInvalidated, Is.False);

            var matched = NewObject("matched", other);
            matched.AddComponent<BoxCollider>();
            matched.transform.SetParent(_root.transform, false);
            NotifyReparent(matched);
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void RemovingAMatchingSubtreeInvalidatesChildQueries(QueryKind kind)
        {
            var child = NewObject("child", _root);
            child.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            child.transform.SetParent(null, false);
            NotifyReparent(child);
            Assert.That(ctx.IsInvalidated, Is.True);
            Assert.That(Query(NewContext(), _root, kind), Is.Empty);
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void DestroyingAMatchingDescendantInvalidatesChildQueries(QueryKind kind)
        {
            var child = NewObject("child", _root);
            child.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);
            var id = child.GetEntityId();

            Object.DestroyImmediate(child);
            ObjectWatcher.Instance.Hierarchy.FireDestroyNotification(id);
            ComputeContext.FlushInvalidates();
            Assert.That(ctx.IsInvalidated, Is.True);
            Assert.That(Query(NewContext(), _root, kind), Is.Empty);
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void ActivenessChangesOnlyInvalidateQueriesExcludingInactiveChildren(QueryKind kind)
        {
            var child = NewObject("child", _root);
            child.AddComponent<BoxCollider>();
            var activeOnly = NewContext();
            var includingInactive = NewContext();
            Query(activeOnly, _root, kind, false);
            Query(includingInactive, _root, kind, true);

            child.SetActive(false);
            NotifyProperties(child);
            Assert.That(activeOnly.IsInvalidated, Is.True);
            Assert.That(includingInactive.IsInvalidated, Is.False);

            var newActiveOnly = NewContext();
            Assert.That(Query(newActiveOnly, _root, kind, false), Is.Empty);
            child.SetActive(true);
            NotifyProperties(child);
            Assert.That(newActiveOnly.IsInvalidated, Is.True);
            Assert.That(includingInactive.IsInvalidated, Is.False);
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void AncestorActivenessChangesInvalidateActiveOnlyChildQueries(QueryKind kind)
        {
            var parent = NewObject("parent");
            _root.transform.SetParent(parent.transform, false);
            var child = NewObject("child", _root);
            child.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind, false);

            parent.SetActive(false);
            NotifyProperties(parent);
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [TestCaseSource(nameof(ChildrenQueries))]
        public void ActivenessChangesWithoutMatchingComponentsDoNotInvalidate(QueryKind kind)
        {
            var child = NewObject("child", _root);
            var ctx = NewContext();
            Query(ctx, _root, kind, false);

            child.SetActive(false);
            NotifyProperties(child);
            Assert.That(ctx.IsInvalidated, Is.False);
        }

        [TestCaseSource(nameof(AllQueries))]
        public void DestroyingTheSearchRootInvalidatesEvenAnEmptyResult(QueryKind kind)
        {
            var ctx = NewContext();
            Query(ctx, _root, kind);
            var id = _root.GetEntityId();

            Object.DestroyImmediate(_root);
            ObjectWatcher.Instance.Hierarchy.FireDestroyNotification(id);
            ComputeContext.FlushInvalidates();
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        [TestCaseSource(nameof(AllQueries))]
        public void ComponentPropertyChangesDoNotInvalidateComponentSearches(QueryKind kind)
        {
            var collider = _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            collider.size = Vector3.one * 2;
            ObjectWatcher.Instance.Hierarchy.FireObjectChangeNotification(collider.GetEntityId());
            ComputeContext.FlushInvalidates();
            Assert.That(ctx.IsInvalidated, Is.False);
        }

        [TestCaseSource(nameof(AllQueries))]
        public void ForcedInvalidationIsNotSuppressedByEqualResults(QueryKind kind)
        {
            _root.AddComponent<BoxCollider>();
            var ctx = NewContext();
            Query(ctx, _root, kind);

            ChangeNotifier.NotifyObjectUpdate(_root);
            ComputeContext.FlushInvalidates();
            Assert.That(ctx.IsInvalidated, Is.True);

            var next = NewContext();
            Query(next, _root, kind);
            _root.AddComponent<SphereCollider>();
            NotifyStructure(_root);
            Assert.That(next.IsInvalidated, Is.False);
        }

        [Test]
        public void InterfaceQueriesCompareComponentReferences()
        {
            var marker = _root.AddComponent<ComponentQueryTestComponent>();
            var ctx = NewContext();
            Assert.That(ctx.GetComponent<IComponentQueryTest>(_root), Is.SameAs(marker));
            Assert.That(ctx.GetComponents<IComponentQueryTest>(_root), Is.EqualTo(new[] { marker }));
            Assert.That(ctx.GetComponentsInChildren<IComponentQueryTest>(_root, true), Is.EqualTo(new[] { marker }));

            _root.AddComponent<SphereCollider>();
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.False);

            Object.DestroyImmediate(marker);
            NotifyStructure(_root);
            Assert.That(ctx.IsInvalidated, Is.True);
        }

        private static IEnumerator SettleEditorChanges()
        {
            yield return null;
            yield return null;
            ComputeContext.FlushInvalidates();
        }

        [UnityTest]
        public IEnumerator ComponentDeletionUndoAndRedoInvalidateFreshObservers()
        {
            var component = _root.AddComponent<BoxCollider>();
            yield return SettleEditorChanges();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            var beforeDelete = NewContext();
            beforeDelete.GetComponents<BoxCollider>(_root);

            Undo.DestroyObjectImmediate(component);
            Undo.CollapseUndoOperations(group);
            yield return SettleEditorChanges();
            Assert.That(beforeDelete.IsInvalidated, Is.True);

            var beforeUndo = NewContext();
            Assert.That(beforeUndo.GetComponents<BoxCollider>(_root), Is.Empty);
            Undo.PerformUndo();
            yield return SettleEditorChanges();
            Assert.That(beforeUndo.IsInvalidated, Is.True);
            Assert.That(_root.GetComponent<BoxCollider>(), Is.Not.Null);

            var beforeRedo = NewContext();
            beforeRedo.GetComponents<BoxCollider>(_root);
            Undo.PerformRedo();
            yield return SettleEditorChanges();
            Assert.That(beforeRedo.IsInvalidated, Is.True);
            Assert.That(_root.GetComponent<BoxCollider>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SearchRootDeletionUndoAndRedoDoNotReuseStaleObservers()
        {
            yield return SettleEditorChanges();
            var id = _root.GetEntityId();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            var beforeDelete = NewContext();
            beforeDelete.GetComponents<BoxCollider>(_root);

            Undo.DestroyObjectImmediate(_root);
            Undo.CollapseUndoOperations(group);
            yield return SettleEditorChanges();
            Assert.That(beforeDelete.IsInvalidated, Is.True);

            Undo.PerformUndo();
            yield return SettleEditorChanges();
            var restored = UnityObjectIDHelper.EntityIdToObject(id) as GameObject;
            Assert.That(restored, Is.Not.Null);
            _root = restored;
            if (!_objects.Any(obj => ReferenceEquals(obj, restored))) _objects.Add(restored);

            var beforeRedo = NewContext();
            Assert.That(beforeRedo.GetComponents<BoxCollider>(restored), Is.Empty);
            Undo.PerformRedo();
            yield return SettleEditorChanges();
            Assert.That(beforeRedo.IsInvalidated, Is.True);
        }
    }
}
