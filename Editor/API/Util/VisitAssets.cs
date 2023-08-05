using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.build_framework.util
{
    public static class VisitAssets
    {
        public static IEnumerable<UnityEngine.Object> ReferencedAssets(
            this UnityEngine.Object root,
            bool traverseSaved = true,
            bool includeScene = true
        )
        {
            HashSet<UnityEngine.Object> visited = new HashSet<Object>();
            Queue<UnityEngine.Object> queue = new Queue<Object>();

            if (root is GameObject go)
            {
                root = go.transform;
            }
            
            visited.Add(root);
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
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
                        if (visited.Add(child))
                        {
                            queue.Enqueue(child);
                        }
                    }
                    
                    foreach (var comp in t.GetComponents<Component>())
                    {
                        if (visited.Add(comp) && !(comp is Transform))
                        {
                            queue.Enqueue(comp);
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
                        && (objIsScene || traverseSaved || !EditorUtility.IsPersistent(value))
                        && visited.Add(value)
                    )
                    {
                        queue.Enqueue(value);
                    }
                }
            }
        }
    }

    public static class WalkObjectProps
    {
        public static IEnumerable<SerializedProperty> ObjectProperties(this SerializedObject obj)
        {
            var target = obj.targetObject;
            if (target is Mesh || target is AnimationClip || target is Texture)
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
                if (prop.propertyType == SerializedPropertyType.String)
                {
                    enterChildren = false;
                    continue;
                }

                if (prop.isArray && IsPrimitiveArray(prop))
                {
                    enterChildren = false;
                    continue;
                }
                
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }
                
                yield return prop;
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