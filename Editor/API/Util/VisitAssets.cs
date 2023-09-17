#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.util
{
    /// <summary>
    /// This class provides helpers to traverse assets or asset properties referenced from a given root object.
    /// </summary>
    public static class VisitAssets
    {
        public delegate bool AssetFilter(Object obj);

        /// <summary>
        /// Returns an enumerable of all assets referenced by the given root object.
        /// </summary>
        /// <param name="root">The asset to start traversal from</param>
        /// <param name="traverseSaved">If false, traversal will not return assets that are saved</param>
        /// <param name="includeScene">If false, scene assets will not be returned</param>
        /// <param name="traversalFilter">If provided, this filter will be queried for each object encountered; if it
        /// returns false, the selected object and all objects referenced from it will be ignored.</param>
        /// <returns>An enumerable of objects found</returns>
        public static IEnumerable<Object> ReferencedAssets(
            this Object root,
            bool traverseSaved = true,
            bool includeScene = true,
            AssetFilter traversalFilter = null
        )
        {
            int index = 0;

            HashSet<Object> visited = new HashSet<Object>();
            Queue<(int, Object)> queue = new Queue<(int, Object)>();

            if (traversalFilter == null)
            {
                traversalFilter = obj => true;
            }

            if (root is GameObject go)
            {
                root = go.transform;
            }

            visited.Add(root);
            queue.Enqueue((index++, root));

            while (queue.Count > 0)
            {
                var (_originalIndex, next) = queue.Dequeue();
                var isScene = next is GameObject || next is Component;

                if (includeScene || !isScene)
                {
                    yield return next;
                }

                if (next is Transform t)
                {
                    if (includeScene)
                    {
                        yield return t.gameObject;
                    }

                    foreach (Transform child in t)
                    {
                        if (t == null) continue; // How can this happen???

                        if (visited.Add(child) && traversalFilter(child.gameObject))
                        {
                            queue.Enqueue((index++, child));
                        }
                    }

                    foreach (var comp in t.GetComponents<Component>())
                    {
                        if (comp == null)
                        {
                            continue; // missing scripts
                        }

                        if (visited.Add(comp) && !(comp is Transform) && traversalFilter(comp))
                        {
                            queue.Enqueue((index++, comp));
                        }
                    }

                    continue;
                }

                foreach (var prop in new SerializedObject(next).ObjectProperties())
                {
                    var value = prop.objectReferenceValue;
                    if (value == null) continue;

                    var objIsScene = value is GameObject || value is Component;

                    if (value != null
                        && !objIsScene
                        && (traverseSaved || !EditorUtility.IsPersistent(value))
                        && visited.Add(value)
                        && traversalFilter(value)
                       )
                    {
                        queue.Enqueue((index++, value));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Provides helpers to walk the properties of a SerializedObject
    /// </summary>
    public static class WalkObjectProps
    {
        /// <summary>
        /// Returns an enumerable that will return _most_ of the properties of a SerializedObject. In particular, this
        /// skips the contents of certain large arrays (e.g. the character contents of strings and the contents of
        /// AnimationClip curves).
        /// </summary>
        /// <param name="obj">The SerializedObject to traverse</param>
        /// <returns>The SerializedProperties found</returns>
        public static IEnumerable<SerializedProperty> AllProperties(this SerializedObject obj)
        {
            var target = obj.targetObject;
            if (target is Mesh || target is Texture)
            {
                // Skip iterating objects with heavyweight internal arrays
                yield break;
            }

            if (target is Transform || target is GameObject)
            {
                // Don't muck around with unity internal stuff here...
                yield break;
            }

            SerializedProperty prop = obj.GetIterator();
            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = true;
                if (prop.name == "m_GameObject") continue;
                if (target is AnimationClip && prop.name == "curve")
                {
                    // Skip the contents of animation curves as they can be quite large and are generally uninteresting
                    enterChildren = false;
                }

                if (prop.propertyType == SerializedPropertyType.String)
                {
                    enterChildren = false;
                }

                if (prop.isArray && IsPrimitiveArray(prop))
                {
                    enterChildren = false;
                }

                yield return prop;
            }
        }

        /// <summary>
        /// Returns all ObjectReference properties of a SerializedObject
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IEnumerable<SerializedProperty> ObjectProperties(this SerializedObject obj)
        {
            foreach (var prop in obj.AllProperties())
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    yield return prop;
                }
            }
        }

        private static bool IsPrimitiveArray(SerializedProperty prop)
        {
            if (prop.arraySize == 0) return false;
            var propertyType = prop.GetArrayElementAtIndex(0).propertyType;
            switch (propertyType)
            {
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ObjectReference:
                    return false;
                default:
                    return true;
            }
        }
    }
}