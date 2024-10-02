#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

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

                
                if (!SamplerCache.TryGetValue(next.GetType(), out var sampler))
                {
                    sampler = CustomSampler.Create("ObjectReferences." + next.GetType());
                    SamplerCache[next.GetType()] = sampler;
                }
                
                sampler.Begin(next);
                foreach (var referenced in ObjectReferences(next))
                {
                    MaybeEnqueueObject(referenced);
                }
                sampler.End();
            }

            void MaybeEnqueueObject(Object value)
            {
                if (value == null) return;
                
                var objIsScene = value is GameObject || value is Component;

                if (!objIsScene
                    && (traverseSaved || !EditorUtility.IsPersistent(value))
                    && visited.Add(value)
                    && traversalFilter(value)
                   )
                {
                    queue.Enqueue((index++, value));
                }
            }
        }

        private static Dictionary<Type, CustomSampler> SamplerCache = new();
        
        private static IEnumerable<Object> ObjectReferences(Object obj)
        {
            if (obj == null) yield break;

            // We have special cases here for a bunch of popular asset types, because SerializedObject traversal is slow.
            // For unrecognized stuff (e.g. VRChat MonoBehaviors) we fall back to SerializedObject.
            switch (obj)
            {
                case Mesh:
                case Shader:
                case Avatar: // Humanoid avatar descriptors have no subassets
                    break;
                case AnimationClip clip:
                {
                    var pptrCurves = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    foreach (var curve in pptrCurves)
                    {
                        var frames = AnimationUtility.GetObjectReferenceCurve(clip, curve);
                        foreach (var frame in frames)
                        {
                            yield return frame.value;
                        }
                    }

                    break;
                }
                case BlendTree tree:
                {
                    foreach (var child in tree.children)
                    {
                        yield return child.motion;
                    }

                    break;
                }
                case AnimatorState state:
                {
                    yield return state.motion;
                    foreach (var b in state.behaviours ?? Array.Empty<StateMachineBehaviour>())
                    {
                        yield return b;
                    }

                    foreach (var t in state.transitions ?? Array.Empty<AnimatorStateTransition>())
                    {
                        yield return t;
                    }

                    break;
                }
                case Material m:
                {
                    /* This approach actually seems slower than using SerializedProperty...
                     var ids = m.GetTexturePropertyNameIDs();
                    foreach (var id in ids)
                    {
                        yield return m.GetTexture(id);
                    }*/
                    
                    // But we can be more efficient and only look at texture props
                    var so = new SerializedObject(m);
                    var texEnvs = so.FindProperty("m_SavedProperties")
                        ?.FindPropertyRelative("m_TexEnvs");

                    if (texEnvs == null || !texEnvs.isArray)
                    {
                        break;
                    }
                    
                    var size = texEnvs.arraySize;

                    for (var i = 0; i < size; ++i)
                    {
                        var texEnv = texEnvs.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                        var texture = texEnv.FindPropertyRelative("m_Texture");

                        yield return texture.objectReferenceValue;
                    }
                    
                    break;
                }
                case AnimatorStateTransition t:
                {
                    yield return t.destinationState;
                    yield return t.destinationStateMachine;
                    break;
                }
                default:
                {
                    foreach (var prop in new SerializedObject(obj).ObjectProperties())
                    {
                        var value = prop.objectReferenceValue;

                        yield return value;
                    }

                    break;
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

                if (prop.propertyType == SerializedPropertyType.String || prop.propertyType == SerializedPropertyType.AnimationCurve)
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