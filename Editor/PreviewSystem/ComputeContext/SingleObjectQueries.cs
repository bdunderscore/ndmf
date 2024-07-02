#region

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    [PublicAPI]
    public static partial class ComputeContextQueries
    {
        /// <summary>
        /// Monitors a given Unity object for changes, and recomputes when changes are detected.
        ///
        /// This will recompute when properties of the object change, when the object is destroyed, or (in the case of
        /// a GameObject), when the children of the GameObject changed.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Observe<T>(this ComputeContext ctx, T obj) where T : Object
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            ObjectWatcher.Instance.MonitorObjectProps(out var dispose, obj, a => a(), invalidate);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return obj;
        }

        /// <summary>
        /// Observes the full path from the scene root to the given transform. The calling computation will be
        /// re-executed if any of the objects in this path are reparented.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="obj"></param>
        /// <returns>An enumerable of transforms in the path, starting from the leaf.</returns>
        public static IEnumerable<Transform> ObservePath(this ComputeContext ctx, Transform obj)
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            ObjectWatcher.Instance.MonitorObjectPath(out var dispose, obj, i => i(), invalidate);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return FollowPath(obj);

            IEnumerable<Transform> FollowPath(Transform obj)
            {
                while (obj != null)
                {
                    yield return obj;
                    obj = obj.parent;
                }
            }
        }

        /// <summary>
        /// Observes the world space position of a given transform.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="t"></param>
        public static Transform ObserveTransformPosition(this ComputeContext ctx, Transform t)
        {
            foreach (var node in ctx.ObservePath(t))
            {
                ctx.Observe(node);
            }

            return t;
        }

        /// <summary>
        /// Observes whether a given game object and all its parents are active.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool ActiveInHierarchy(this ComputeContext ctx, GameObject obj)
        {
            ObservePath(ctx, obj.transform);
            return obj.activeInHierarchy;
        }

        /// <summary>
        /// Observes whether a component is enabled, and its heirarchy path is active.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool ActiveAndEnabled(this ComputeContext ctx, Behaviour c)
        {
            return ActiveInHierarchy(ctx, c.gameObject) && ctx.Observe(c).enabled;
        }

        public static C GetComponent<C>(this ComputeContext ctx, GameObject obj) where C : Component
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj, a => a(), invalidate,
                () => obj != null ? obj.GetComponent<C>() : null);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }

        public static Component GetComponent(this ComputeContext ctx, GameObject obj, Type type)
        {
            if (obj == null) return null;

            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj, a => a(), invalidate,
                () => obj != null ? obj.GetComponent(type) : (Component)null);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }

        public static C[] GetComponents<C>(this ComputeContext ctx, GameObject obj) where C : Component
        {
            if (obj == null) return Array.Empty<C>();

            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, a => a(), invalidate,
                () => obj != null ? obj.GetComponents<C>() : Array.Empty<C>(), false);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }

        public static Component[] GetComponents(this ComputeContext ctx, GameObject obj, Type type)
        {
            if (obj == null) return Array.Empty<Component>();

            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, a => a(), invalidate,
                () => obj != null ? obj.GetComponents(type) : Array.Empty<Component>(), false);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }

        public static C[] GetComponentsInChildren<C>(this ComputeContext ctx, GameObject obj, bool includeInactive)
            where C : Component
        {
            if (obj == null) return Array.Empty<C>();

            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, a => a(), invalidate,
                () => { return obj != null ? obj.GetComponentsInChildren<C>(includeInactive) : Array.Empty<C>(); },
                true);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }

        public static Component[] GetComponentsInChildren(this ComputeContext ctx, GameObject obj, Type type,
            bool includeInactive)
        {
            if (obj == null) return Array.Empty<Component>();

            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, a => a(), invalidate,
                () => obj != null ? obj.GetComponentsInChildren(type, includeInactive) : Array.Empty<Component>(),
                true);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
    }
}