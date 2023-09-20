using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.ndmf.runtime
{
    /// <summary>
    /// A collection of general-purpose utilities that are available from Runtime-scope scripts.
    /// </summary>
    public static class RuntimeUtil
    {
        /// <summary>
        /// Invoke this function to register a callback with EditorApplication.delayCall from a context that cannot
        /// access EditorApplication.
        /// </summary>
        public static Action<Action> DelayCall { get; internal set; }

        static RuntimeUtil()
        {
            DelayCall = action => { throw new Exception("delayCall() cannot be called during static initialization"); };
        }

        // Shadow the VRC-provided methods to avoid deprecation warnings
        internal static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            var component = obj.GetComponent<T>();
            if (component == null) component = obj.AddComponent<T>();
            return component;
        }

        internal static T GetOrAddComponent<T>(this Component obj) where T : Component
        {
            return obj.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Returns whether the editor is in play mode.
        /// </summary>
#if UNITY_EDITOR
        public static bool IsPlaying => UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
#else
        public static bool IsPlaying => true;
#endif

        /// <summary>
        /// Returns the relative path from root to child, or null is child is not a descendant of root.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        [CanBeNull]
        public static string RelativePath(GameObject root, GameObject child)
        {
            if (root == child) return "";

            List<string> pathSegments = new List<string>();
            while (child != root && child != null)
            {
                pathSegments.Add(child.name);
                child = child.transform.parent?.gameObject;
            }

            if (child == null && root != null) return null;

            pathSegments.Reverse();
            return String.Join("/", pathSegments);
        }

        /// <summary>
        /// Returns the path of a game object relative to the avatar root, or null if the avatar root could not be
        /// located.
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        [CanBeNull]
        public static string AvatarRootPath(GameObject child)
        {
            if (child == null) return null;
            var avatar = FindAvatarInParents(child.transform);
            if (avatar == null) return null;
            return RelativePath(avatar.gameObject, child);
        }

        /// <summary>
        /// Returns the component marking the root of the avatar.
        ///
        /// Internal for now as we need to refactor this to be less VRChat-specific.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        internal static Transform FindAvatarInParents(Transform target)
        {
            while (target != null)
            {
                var av = target.GetComponent<VRCAvatarDescriptor>();
                if (av != null) return av.transform;
                target = target.parent;
            }

            return null;
        }
    }
}