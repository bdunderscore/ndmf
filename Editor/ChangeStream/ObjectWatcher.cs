#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.trace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.cs
{
    #region

    using UnityObject = Object;

    #endregion

    /// <summary>
    /// ObjectWatcher provides a high level API for monitoring for various changes to assets and scene objects in the
    /// editor.
    /// </summary>
    internal sealed class ObjectWatcher
    {
        // Supported watch categories:
        // - Single-object watch: Monitor asset, component properties, etc
        //   -> simple mapping
        // - Parent watch: Monitor whether the parent of an object changes
        //   -> record parent path
        // - Component search: Monitor the set of components matching a type filter under a given object
        //   -> 

        // Event types:
        //   - ChangeScene: Fires everything
        //   - CreateGameObjectHierarchy: Check parents, possibly fire component search notifications
        //     -> May result in creation of new components under existing nodes
        //   - ChangeGameObjectStructureHierarchy: Check old and new parents, possibly fire component search notifications
        //     -> May result in creation of new components under existing nodes, or reparenting of components
        //   - ChangeGameObjectStructure: Check parents, possibly fire component search notifications
        //     -> Creates/deletes components
        //   - ChangeGameObjectOrComponentProperties:
        //     -> If component, fire single notification. If GameObject, this might be a component reordering, so fire
        //        the component search notifications as needed
        //   - CreateAssetObject: Ignored
        //   - DestroyAssetObject: Fire single object notifications
        //   - ChangeAssetObjectProperties: Fire single object notifications
        //   - UpdatePrefabInstances: Treated as ChangeGameObjectStructureHierarchy
        //   - ChangeChildrenOrder: Fire component search notifications

        // High level structure:
        //   We maintain a "shadow hierarchy" of GameObjects with their last known parent/child relationships.
        //   Since OCES doesn't give us the prior state, we need this to determine which parent objects need to be
        //   notified when objects move. Each shadow GameObject also tracks the last known set of components on the object.
        //
        //   Listeners come in two flavors: object listeners (asset/component watches as well as parent watches), and
        //   component search listeners, which can be local or recursive.

        public static ObjectWatcher Instance { get; } = new();
        internal ShadowHierarchy Hierarchy = new();
        internal PropertyMonitor PropertyMonitor = new();
        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;

        internal ObjectWatcher()
        {
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.delayCall += () =>
            {
                EditorSceneManager.sceneOpened += (scene, _) =>
                {
                    if (scene.name == NDMFPreviewSceneManager.PreviewSceneName) return;
                    Instance.Hierarchy.InvalidateAll();
                };

                EditorSceneManager.sceneClosed += _ => Instance.Hierarchy.InvalidateAll();

                EditorSceneManager.newSceneCreated += (_, _, _) => { Instance.Hierarchy.InvalidateAll(); };
                
                Instance.PropertyMonitor.MaybeStartRefreshTimer();
            };
        }

        public ImmutableList<GameObject> MonitorSceneRoots(ComputeContext ctx)
        {
            ImmutableList<GameObject> rootSet = GetRootSet();

            // TODO scene load callbacks

            var cancel = Hierarchy.RegisterRootSetListener(_ =>
            {
                ImmutableList<GameObject> newRootSet = GetRootSet();
                return !newRootSet.SequenceEqual(rootSet);
            }, ctx);

            BindCancel(ctx, cancel);

            return rootSet;
        }

        private ImmutableList<GameObject> GetRootSet()
        {
            ImmutableList<GameObject>.Builder roots = ImmutableList.CreateBuilder<GameObject>();

            var sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.hideFlags != 0)
                    {
                        continue;
                    }
                    roots.Add(go);
                }
            }

            return roots.ToImmutable();
        }

        public void MonitorObjectPath(Transform t, ComputeContext ctx)
        {
            var cancel = Hierarchy.RegisterGameObjectListener(t.gameObject, e =>
            {
                switch (e)
                {
                    case HierarchyEvent.PathChange:
                    case HierarchyEvent.ForceInvalidate:
                        return true;
                    default:
                        return false;
                }
            }, ctx);
            
            Hierarchy.EnablePathMonitoring(t.gameObject);

            BindCancel(ctx, cancel);
        }

        private void BindCancel(ComputeContext ctx, IDisposable cancel)
        {
            cancel = CancelWrapper(cancel);
            ctx.OnInvalidate.ContinueWith(_ => cancel.Dispose());
        }

        public R MonitorObjectProps<T, R>(T obj, ComputeContext ctx, Func<T, R> extract, Func<R, R, bool> compare,
            bool usePropMonitor, [CanBeNull] string file, int? line)
            where T : UnityObject
        {
            var curVal = extract(obj);

            if (obj == null) return curVal;
            if (compare == null) compare = EqualityComparer<R>.Default.Equals;

            if (obj is GameObject go)
            {
                var cancel = Hierarchy.RegisterGameObjectListener(go, e =>
                {
                    switch (e)
                    {
                        case HierarchyEvent.ObjectDirty:
                        case HierarchyEvent.ForceInvalidate:
                            if (obj != null && compare(curVal, extract(obj)))
                            {
                                TraceBuffer.RecordTraceEvent(
                                    "ObjectWatcher.MonitorObjectProps",
                                    ev => $"[{ev.FilePath}:{ev.Line}] Object {ev.Arg0} unchanged",
                                    go.name,
                                    filename: file ?? "???",
                                    line: line ?? 0
                                );
                                return false;
                            }

                            return true;
                        default:
                            return false;
                    }
                }, ctx);

                BindCancel(ctx, cancel);
            }
            else
            {
                object objName = (obj is Component c_) ? c_.gameObject.name : obj;
                
                var cancel = Hierarchy.RegisterObjectListener(obj, e =>
                {
                    switch (e)
                    {
                        case HierarchyEvent.ObjectDirty:
                        case HierarchyEvent.ForceInvalidate:
                            if (obj != null && compare(curVal, extract(obj)))
                            {
                                TraceBuffer.RecordTraceEvent(
                                    "ObjectWatcher.MonitorObjectProps",
                                    ev => $"[{ev.FilePath}:{ev.Line}] Object {ev.Arg0} unchanged",
                                    objName,
                                    filename: file ?? "???",
                                    line: line ?? 0
                                );
                                return false;
                            }

                            return true;
                        default:
                            return false;
                    }
                }, ctx);

                BindCancel(ctx, cancel);

                if (usePropMonitor)
                {
                    if (obj is Component c && c.gameObject.hideFlags != 0) return curVal;

                    var propsListeners = PropertyMonitor.MonitorObjectProps(obj);
                    propsListeners.Register(_ => obj == null || !compare(curVal, extract(obj)), ctx);

                    BindCancel(ctx, cancel);
                }
            }

            return curVal;
        }

        private static void InvokeCallback<T>(Action<T> callback, object t) where T : class
        {
            try
            {
                callback((T)t);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }


        private static bool InvokeCallback<T>(Func<T, bool> callback, object t) where T : class
        {
            try
            {
                return callback((T)t);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return true;
            }
        }

        public void MonitorGetComponents(GameObject obj, ComputeContext ctx, bool includeChildren)
        {
            var previewScene = NDMFPreviewSceneManager.GetPreviewScene();

            // Ensure component structure data is up-to-date
            Hierarchy.RequestComponentStructureCheck(obj.GetInstanceID());
            
            var cancel = Hierarchy.RegisterGameObjectListener(obj, e =>
            {
                if (e == HierarchyEvent.ChildComponentsChanged && !includeChildren) return false;

                switch (e)
                {
                    case HierarchyEvent.ChildComponentsChanged:
                        return includeChildren;
                    case HierarchyEvent.SelfComponentsChanged:
                    case HierarchyEvent.ForceInvalidate:
                        return true;
                    default:
                        return false;
                }
            }, ctx);

            if (includeChildren) Hierarchy.EnableComponentMonitoring(obj);

            BindCancel(ctx, cancel);
        }
        
        class WrappedDisposable : IDisposable
        {
            private readonly int _targetThread;
            private readonly SynchronizationContext _syncContext;
            private IDisposable[] _orig;

            public WrappedDisposable(IDisposable[] orig, SynchronizationContext syncContext)
            {
                _orig = orig;
                _targetThread = Thread.CurrentThread.ManagedThreadId;
                _syncContext = syncContext;
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (_orig == null) return;

                    if (Thread.CurrentThread.ManagedThreadId == _targetThread)
                    {
                        DoDispose(_orig);
                    }
                    else
                    {
                        NDMFSyncContext.Context.Post(targets => DoDispose((IDisposable[])targets), _orig);
                    }

                    _orig = null;
                }
            }

            private static void DoDispose(IDisposable[] targets)
            {
                foreach (var orig in targets)
                {
                    orig.Dispose();
                }
            }
        }

        private IDisposable CancelWrapper(params IDisposable[] orig)
        {
            return new WrappedDisposable(orig, _syncContext);
        }
    }
}