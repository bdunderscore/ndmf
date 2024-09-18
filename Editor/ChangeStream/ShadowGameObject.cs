#region

#if NDMF_DEBUG
using System.Text;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.cs
{
    #region

    using UnityObject = Object;

    #endregion

    internal enum HierarchyEvent
    {
        /// <summary>
        /// Fired when an unspecified changed may have happened to this object.
        /// </summary>
        ObjectDirty,

        /// <summary>
        /// Fired when the parentage of this object has changed.
        /// </summary>
        PathChange,

        /// <summary>
        /// Fired when the set or order of components on this object may have changed
        /// </summary>
        SelfComponentsChanged,

        /// <summary>
        /// Fired when the set or order of components on this object or any children may have changed
        /// </summary>
        ChildComponentsChanged,

        /// <summary>
        /// Fired when an object is destroyed or otherwise we're not quite sure what's going on.
        /// </summary>
        ForceInvalidate,
    }

    internal class ShadowHierarchy
    {
        internal SynchronizationContext _syncContext;
        internal Dictionary<int, ShadowGameObject> _gameObjects = new();
        internal Dictionary<int, ShadowObject> _otherObjects = new();
        internal ListenerSet<HierarchyEvent> _rootSetListener = new();

        int lastPruned = Int32.MinValue;

#if NDMF_DEBUG
        [MenuItem("Tools/NDM Framework/Debug Tools/Dump shadow hierarchy")]
        static void StaticDumpShadowHierarchy()
        {
            ObjectWatcher.Instance.Hierarchy.DumpShadowHierarchy();
        }
        
        void DumpShadowHierarchy()
        {
            int indent = 0;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("[Shadow Hierarchy Dump]");
            sb.AppendLine("Root set listeners:");
            indent += 2;
            DumpListenerSet(_rootSetListener);
            sb.AppendLine();
            indent -= 2;
            
            sb.AppendLine("GameObjects:");
            foreach (var obj in _gameObjects.Values)
            {
                if (obj.Parent == null)
                {
                    DumpShadowGameObject(obj);
                }
            }
            
            sb.AppendLine("Other objects:");
            foreach (var obj in _otherObjects.Values)
            {
                DumpShadowObject(obj);
            }
            
            sb.AppendLine("[End Shadow Hierarchy Dump]");
            
            UnityEngine.Debug.Log(sb.ToString());
            
            void DumpShadowObject(ShadowObject obj)
            {
                sb.Append(' ', indent);
                sb.Append("+ ");
                if (obj.Object == null)
                {
                    sb.AppendLine("<" + obj.InstanceID + ">");
                }
                else if (obj.Object is Component c)
                {
                    sb.AppendLine(c.gameObject.name + " (" + c.GetType().Name + ")");
                }
                else
                {
                    sb.AppendLine(obj.Object.name);
                }
            }
            
            void DumpShadowGameObject(ShadowGameObject obj)
            {
                sb.Append(' ', indent);
                sb.Append("+ ");
                sb.AppendLine(obj.GameObject == null ? "<" + obj.InstanceID + ">" : obj.GameObject.name);
                indent += 2;
                DumpListenerSet(obj._listeners);
                sb.AppendLine();
                sb.Append(' ', indent);
                sb.AppendLine("Children:");
                foreach (var child in obj.Children)
                {
                    DumpShadowGameObject(child);
                }
                indent -= 2;
            }

            void DumpListenerSet<T>(ListenerSet<T> set)
            {
                foreach (var listener in set.GetListeners())
                {
                    sb.Append(' ', indent);
                    sb.AppendLine(listener.ToString());
                }
            }
        }
#endif
        
        internal IDisposable RegisterRootSetListener(ListenerSet<HierarchyEvent>.Filter filter, ComputeContext ctx)
        {
#if NDMF_TRACE
            Debug.WriteLine("[ShadowHierarchy] RegisterRootSetListener()");
#endif
            
            if (ctx.IsInvalidated) return new NullDisposable();
            
            return _rootSetListener.Register(filter, ctx);
        }

        internal IDisposable RegisterGameObjectListener(
            GameObject targetObject,
            ListenerSet<HierarchyEvent>.Filter filter,
            ComputeContext ctx
        )
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] RegisterGameObjectListener({targetObject.GetInstanceID()})");
#endif
            
            if (targetObject == null || ctx.IsInvalidated) return new NullDisposable();

            ShadowGameObject shadowObject = ActivateShadowObject(targetObject);

            return shadowObject._listeners.Register(filter, ctx);
        }

        internal IDisposable RegisterObjectListener(UnityObject targetComponent,
            ListenerSet<HierarchyEvent>.Filter filter,
            ComputeContext ctx
        )
        {
            if (targetComponent == null || ctx.IsInvalidated) return new NullDisposable();
            
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] RegisterObjectListener({targetComponent.GetInstanceID()})");
#endif

            if (!_otherObjects.TryGetValue(targetComponent.GetInstanceID(), out var shadowComponent))
            {
                shadowComponent = new ShadowObject(targetComponent);
                _otherObjects[targetComponent.GetInstanceID()] = shadowComponent;
            }

            return shadowComponent._listeners.Register(filter, ctx);
        }

        internal class NullDisposable : IDisposable
        {
            public void Dispose()
            {
                // no-op
            }
        }

        /// <summary>
        /// Activates monitoring for all children of the specified GameObject. This is needed to ensure child component
        /// change notifications are propagated correctly.
        /// </summary>
        /// <param name="root"></param>
        internal void EnableComponentMonitoring(GameObject root)
        {
#if NDMF_TRACE
            //Debug.WriteLine($"[ShadowHierarchy] EnableComponentMonitoring({root.GetInstanceID()})");
#endif
            
            var obj = ActivateShadowObject(root);

            EnableComponentMonitoring(obj);
        }

        private void EnableComponentMonitoring(ShadowGameObject obj)
        {
            if (obj.ComponentMonitoring) return;

            foreach (Transform child in obj.GameObject.transform)
            {
                EnableComponentMonitoring(child.gameObject);
            }

            // Enable the parent last to avoid nuisance notifications
            obj.ComponentMonitoring = true;
        }

        internal void EnablePathMonitoring(GameObject root)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] EnablePathMonitoring({root.GetInstanceID()})");
#endif
            
            var obj = ActivateShadowObject(root);

            while (obj != null)
            {
                obj.PathMonitoring = true;
                obj = obj.Parent;
            }
        }

        private ShadowGameObject ActivateShadowObject(GameObject targetObject)
        {
            // An object is activated when it, or a parent, has a listener attached.
            // An object is deactivated ("inert") when we traverse it and find no listeners in any of its children.
            // Inert objects are skipped for path update notifications; however, we can't just delete them, because
            // we may need to know about them for future structure change notifications at their parents.
            int instanceId = targetObject.GetInstanceID();
            if (!_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                shadow = new ShadowGameObject(targetObject);
                _gameObjects[instanceId] = shadow;

                shadow.Scene = targetObject.scene;
                var parent = targetObject.transform.parent?.gameObject;
                if (parent == null)
                {
                    shadow.SetParent(null, false);
                    _rootSetListener.Fire(HierarchyEvent.ForceInvalidate);
                }
                else
                {
                    // Don't fire notifications on initial creation
                    shadow.SetParent(ActivateShadowObject(parent), false);
                }
            }

            if (shadow.Parent?.ComponentMonitoring == true && !shadow.ComponentMonitoring)
            {
                EnableComponentMonitoring(shadow);
                FireParentComponentChangeNotifications(shadow.Parent);
            }

            return shadow;
        }

        /// <summary>
        /// Fires a notification that properties on a specific object (GameObject or otherwise) has changed.
        /// </summary>
        /// <param name="instanceId"></param>
        internal void FireObjectChangeNotification(int instanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] FireObjectChangeNotification({instanceId})");
#endif
            
            if (_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                shadow._listeners.Fire(HierarchyEvent.ObjectDirty);
                if (shadow.IsActive != shadow.GameObject.activeSelf)
                {
                    shadow.IsActive = shadow.GameObject.activeSelf;
                    FirePathChangeNotifications(shadow);
                }
            }

            var component = EditorUtility.InstanceIDToObject(instanceId) as Component;
            if (component != null)
            {
                // This event may have been a component reordering, so trigger a synthetic structure change event.
                // TODO: Cache component positions?
                var parentId = component.gameObject.GetInstanceID();
                FireStructureChangeEvent(parentId);
            }

            if (_otherObjects.TryGetValue(instanceId, out var shadowComponent))
            {
                shadowComponent._listeners.Fire(HierarchyEvent.ObjectDirty);
            }
        }

        /// <summary>
        /// Fires a notification that the specified GameObject has a new parent.
        /// </summary>
        /// <param name="instanceId"></param>
        internal void FireReparentNotification(int instanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] FireReparentNotification({instanceId})");
#endif
            
            // Always activate on reparent. This is because we might be reparenting _into_ an active hierarchy.
            var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            ShadowGameObject shadow;
            if (obj != null)
            {
                shadow = ActivateShadowObject(obj);
            }
            else
            {
                if (_gameObjects.TryGetValue(instanceId, out var _))
                {
                    FireDestroyNotification(instanceId);
                }

                return;
            }

            FireParentComponentChangeNotifications(shadow.Parent);
            if (shadow.PathMonitoring) FirePathChangeNotifications(shadow);

            // Ensure the new parent is marked as dirty, in case this is a new object and we suppressed the dirty
            // notifications.
            if (shadow.Parent != null) shadow.Parent._listeners.Fire(HierarchyEvent.ObjectDirty);

            // Update parentage and refire

            var newParent = shadow.GameObject.transform.parent?.gameObject;
            if (newParent == null)
            {
                shadow.Parent = null;
                _rootSetListener.Fire(HierarchyEvent.ForceInvalidate);
            }
            else if (newParent != shadow.Parent?.GameObject)
            {
                if (shadow.Parent == null) _rootSetListener.Fire(HierarchyEvent.ForceInvalidate);

                shadow.Parent = ActivateShadowObject(newParent);
                FireParentComponentChangeNotifications(shadow.Parent);

                var ptr = shadow.Parent;
                while (ptr != null && !ptr.PathMonitoring)
                {
                    ptr.PathMonitoring = true;
                    ptr = ptr.Parent;
                }
            }

            // This needs to run even if the parent did not change, just in case we did a just-in-time creation of this
            // shadow object.
            if (shadow.Parent?.ComponentMonitoring == true) EnableComponentMonitoring(shadow);
        }

        private void FirePathChangeNotifications(ShadowGameObject shadow)
        {
            if (!shadow.PathMonitoring) return;
            shadow._listeners.Fire(HierarchyEvent.PathChange);
            foreach (var child in shadow.Children)
            {
                FirePathChangeNotifications(child);
            }
        }

        private void FireParentComponentChangeNotifications(ShadowGameObject obj)
        {
            while (obj != null)
            {
                obj._listeners.Fire(HierarchyEvent.ChildComponentsChanged);
                obj = obj.Parent;
            }
        }

        internal void FireDestroyNotification(int instanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] FireDestroyNotification({instanceId})");
#endif
            
            if (_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                FireParentComponentChangeNotifications(shadow.Parent);
                ForceInvalidateHierarchy(shadow);
            }
        }

        void ForceInvalidateHierarchy(ShadowGameObject obj)
        {
#if NDMF_TRACE
            var resolvedName = obj.GameObject == null ? "<null>" : obj.GameObject.name;
            Debug.WriteLine($"[ShadowHierarchy] ForceInvalidateHierarchy({obj.InstanceID}:{resolvedName})");
#endif
            
            obj._listeners.Fire(HierarchyEvent.ForceInvalidate);
            _gameObjects.Remove(obj.InstanceID);

            foreach (var child in obj.Children)
            {
                ForceInvalidateHierarchy(child);
            }
        }

        internal void FireReorderNotification(int parentInstanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] FireReorderNotification({parentInstanceId})");
#endif
            
            if (!_gameObjects.TryGetValue(parentInstanceId, out var shadow))
            {
                return;
            }

            FireParentComponentChangeNotifications(shadow);
        }

        internal void FireStructureChangeEvent(int instanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] FireStructureChangeEvent({instanceId})");
#endif
            
            if (!_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                return;
            }

            shadow._listeners.Fire(HierarchyEvent.SelfComponentsChanged);
            FireParentComponentChangeNotifications(shadow.Parent);
        }

        internal void InvalidateAll()
        {
#if NDMF_TRACE
            Debug.WriteLine("[ShadowHierarchy] InvalidateAll()");
#endif
            
            var oldDict = _gameObjects;
            _gameObjects = new Dictionary<int, ShadowGameObject>();

            foreach (var shadow in oldDict.Values)
            {
                shadow._listeners.FireAll();
            }

            var oldComponents = _otherObjects;
            _otherObjects = new Dictionary<int, ShadowObject>();

            foreach (var shadow in oldComponents.Values)
            {
                shadow._listeners.FireAll();
            }

            _rootSetListener.FireAll();
        }

        /// <summary>
        /// Assume that everything has changed for the specified object and its children. Fire off all relevant
        /// notifications and rebuild state.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void InvalidateTree(int instanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] InvalidateTree({instanceId})");
#endif
            
            if (_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                _gameObjects.Remove(instanceId);
                shadow._listeners.Fire(HierarchyEvent.ForceInvalidate);
                FireParentComponentChangeNotifications(shadow.Parent);

                var parentGameObject = shadow.Parent?.GameObject;

                if (parentGameObject != null)
                {
                    // Repair parent's child mappings
                    foreach (Transform child in parentGameObject.transform)
                    {
                        ActivateShadowObject(child.gameObject);
                    }
                }

                // Finally recreate the target object, just in case it took up some objects from somewhere else
                var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (gameObject != null)
                {
                    ActivateShadowObject(gameObject);
                }
            }

            if (_otherObjects.TryGetValue(instanceId, out var otherObj))
            {
                _otherObjects.Remove(instanceId);
                otherObj._listeners.Fire(HierarchyEvent.ForceInvalidate);
            }
        }

        public void FireGameObjectCreate(int instanceId)
        {
#if NDMF_TRACE
            Debug.WriteLine($"[ShadowHierarchy] FireGameObjectCreate({instanceId})");
#endif
            
            var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (obj == null) return;

            var shadow = ActivateShadowObject(obj);

            // Ensure the new parent is marked as dirty
            if (shadow.Parent != null) shadow.Parent._listeners.Fire(HierarchyEvent.ObjectDirty);
        }
    }

    internal class ShadowObject
    {
        internal int InstanceID { get; private set; }
        internal UnityObject Object { get; private set; }

        internal ListenerSet<HierarchyEvent> _listeners = new ListenerSet<HierarchyEvent>();

        internal ShadowObject(UnityObject component)
        {
            InstanceID = component.GetInstanceID();
            Object = component;
        }
    }

    /// <summary>
    /// Represents a single GameObject in a loaded scene. This shadow copy will be retained, once an interest is
    /// registered, until the GameObject is destroyed or scene unloaded.
    /// </summary>
    internal class ShadowGameObject
    {
        internal int InstanceID { get; private set; }
        internal GameObject GameObject { get; private set; }
        internal Scene Scene { get; set; }
        private readonly Dictionary<int, ShadowGameObject> _children = new Dictionary<int, ShadowGameObject>();

        public IEnumerable<ShadowGameObject> Children => _children.Values;


        private ShadowGameObject _parent;
        internal bool PathMonitoring { get; set; } = false;
        internal bool ComponentMonitoring { get; set; } = false;
        internal bool IsActive { get; set; }

        internal ShadowGameObject Parent
        {
            get => _parent;
            set { SetParent(value, true); }
        }


        public void SetParent(ShadowGameObject parent, bool fireNotifications = true)
        {
            if (parent == _parent) return;

            if (_parent != null)
            {
                _parent._children.Remove(InstanceID);
                // Fire off a property change notification for the parent itself
                // TODO: tests
                if (fireNotifications) _parent._listeners.Fire(HierarchyEvent.ObjectDirty);
            }

            _parent = parent;

            if (_parent != null)
            {
                _parent._children[InstanceID] = this;
                if (fireNotifications) _parent._listeners.Fire(HierarchyEvent.ObjectDirty);
            }
        }

        internal ListenerSet<HierarchyEvent> _listeners = new ListenerSet<HierarchyEvent>();

        internal ShadowGameObject(GameObject gameObject)
        {
            InstanceID = gameObject.GetInstanceID();
            GameObject = gameObject;
            Scene = gameObject.scene;
            IsActive = gameObject.activeSelf;
        }
    }
}