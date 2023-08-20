using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.build_framework.util;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.build_framework.animation
{
    using UnityObject = UnityEngine.Object;
    /// <summary>
    /// This extension context tracks when objects are renamed, and updates animations accordingly.
    /// Users of this context need to be aware that, when creating new curves (or otherwise introducing new motions,
    /// use context.ObjectPath to obtain a suitable path for the target objects).
    /// </summary>
    public sealed class TrackObjectRenamesContext : ExtensionContext
    {
        private Dictionary<GameObject, List<string>> _objectToOriginalPaths = new Dictionary<GameObject, List<string>>();
        private HashSet<GameObject> _transformLookthroughObjects = new HashSet<GameObject>();
        private ImmutableDictionary<string, string> _originalPathToMappedPath = null;
        private ImmutableDictionary<string, string> _transformOriginalPathToMappedPath = null;
        
        public void OnActivate(BuildContext context)
        {
            _objectToOriginalPaths.Clear();
            _transformLookthroughObjects.Clear();
            ClearCache();

            foreach (var xform in context.AvatarRootTransform.GetComponentsInChildren<Transform>(true))
            {
                _objectToOriginalPaths.Add(xform.gameObject, new List<string> { xform.gameObject.AvatarRootPath() });
            }
        }

        private void ClearCache()
        {
            _originalPathToMappedPath = null;
            _transformOriginalPathToMappedPath = null;
        }

        /// <summary>
        /// Sets the "transform lookthrough" flag for an object. Any transform animations on this object will be
        /// redirected to its parent. This is used in Modular Avatar as part of bone merging logic.
        /// </summary>
        /// <param name="obj"></param>
        public void MarkTransformLookthrough(GameObject obj)
        {
            _transformLookthroughObjects.Add(obj);
        }

        /// <summary>
        /// Returns a path for use in dynamically generated animations for a given object. This can include objects not
        /// present at the time of context activation; in this case, they will be assigned a randomly-generated internal
        /// path and replaced during path remapping with the true path.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public string GetObjectIdentifier(GameObject obj)
        {
            if (_objectToOriginalPaths.TryGetValue(obj, out var paths))
            {
                return paths[0];
            }
            else
            {
                var internalPath = "_NewlyCreatedObject/" + GUID.Generate() + "/" + obj.AvatarRootPath();
                _objectToOriginalPaths.Add(obj, new List<string> {internalPath});
                return internalPath;
            }
        }
        
        /// <summary>
        /// Marks an object as having been removed. Its paths will be remapped to its parent. 
        /// </summary>
        /// <param name="obj"></param>
        public void MarkRemoved(GameObject obj)
        {
            ClearCache();
            if (_objectToOriginalPaths.TryGetValue(obj, out var paths))
            {
                var parent = obj.transform.parent.gameObject;
                if (_objectToOriginalPaths.TryGetValue(parent, out var parentPaths))
                {
                    parentPaths.AddRange(paths);
                }

                _objectToOriginalPaths.Remove(obj);
                _transformLookthroughObjects.Remove(obj);
            }
        }

        
        /// <summary>
        /// Marks an object as having been replaced by another object. All references to the old object will be replaced
        /// by the new object. References originally to the new object will continue to point to the new object.
        /// </summary>
        /// <param name="old"></param>
        /// <param name="newObject"></param>
        public void ReplaceObject(GameObject old, GameObject newObject)
        {
            ClearCache();

            if (_objectToOriginalPaths.TryGetValue(old, out var paths))
            {
                if (!_objectToOriginalPaths.TryGetValue(newObject, out var newObjectPaths))
                {
                    newObjectPaths = new List<string>();
                    _objectToOriginalPaths.Add(newObject, newObjectPaths);
                }

                newObjectPaths.AddRange(paths);

                _objectToOriginalPaths.Remove(old);
            }


            if (_transformLookthroughObjects.Contains(old))
            {
                _transformLookthroughObjects.Remove(old);
                _transformLookthroughObjects.Add(newObject);
            }
        }
        
        
        private ImmutableDictionary<string, string> BuildMapping(ref ImmutableDictionary<string, string> cache,
            bool transformLookup)
        {
            if (cache != null) return cache;

            ImmutableDictionary<string, string> dict = ImmutableDictionary<string, string>.Empty;

            foreach (var kvp in _objectToOriginalPaths)
            {
                var obj = kvp.Key;
                var paths = kvp.Value;

                if (transformLookup)
                {
                    while (_transformLookthroughObjects.Contains(obj))
                    {
                        obj = obj.transform.parent.gameObject;
                    }
                }

                var newPath = obj.AvatarRootPath();
                foreach (var origPath in paths)
                {
                    if (!dict.ContainsKey(origPath))
                    {
                        dict = dict.Add(origPath, newPath);
                    }
                }
            }

            cache = dict;
            return cache;
        }

        internal string MapPath(string path, bool isTransformMapping = false)
        {
            ImmutableDictionary<string, string> mappings;

            if (isTransformMapping)
            {
                mappings = BuildMapping(ref _originalPathToMappedPath, true);
            }
            else
            {
                mappings = BuildMapping(ref _transformOriginalPathToMappedPath, false);
            }

            if (mappings.TryGetValue(path, out var mappedPath))
            {
                return mappedPath;
            }
            else
            {
                return path;
            }
        }

        public RuntimeAnimatorController ApplyMappingsToAnimator(
            RuntimeAnimatorController controller,
            Dictionary<AnimationClip, AnimationClip> clipCache = null)
        {
            if (clipCache == null)
            {
                clipCache = new Dictionary<AnimationClip, AnimationClip>();
            }
            
            if (controller == null) return null;
            
            switch (controller)
            {
                case AnimatorController ac:
                    ac = AnimationUtil.DeepCloneAnimator(ac);
                    foreach (var asset in ac.ReferencedAssets())
                    {
                        if (asset is AnimatorState state)
                        {
                            if (state.motion is AnimationClip clip)
                            {
                                state.motion = ApplyMappingsToClip(clip, clipCache);
                            }
                        } else if (asset is BlendTree tree)
                        {
                            var children = tree.children;
                            for (int i = 0; i < children.Length; i++)
                            {
                                var child = children[i];
                                if (child.motion is AnimationClip clip)
                                {
                                    child.motion = ApplyMappingsToClip(clip, clipCache);
                                }
                            }

                            tree.children = children;
                        }
                    }

                    return ac;
                case AnimatorOverrideController aoc:
                {
                    AnimatorOverrideController newController = new AnimatorOverrideController();
                    newController.runtimeAnimatorController = ApplyMappingsToAnimator(aoc.runtimeAnimatorController);
                    List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                        new List<KeyValuePair<AnimationClip, AnimationClip>>();

                    overrides = overrides.Select(kvp =>
                        new KeyValuePair<AnimationClip, AnimationClip>(kvp.Key, ApplyMappingsToClip(kvp.Value, clipCache)))
                        .ToList();
                    
                    newController.ApplyOverrides(overrides);
                    
                    return newController;
                }
                default:
                    throw new Exception("Unknown animator controller type: " + controller.GetType().Name);
            }
        }

        private AnimationClip ApplyMappingsToClip(AnimationClip originalClip, Dictionary<AnimationClip, AnimationClip> clipCache = null)
        {
            if (originalClip == null) return null;
            if (clipCache?.TryGetValue(originalClip, out var cachedClip) == true) return cachedClip;
            
            var clip = UnityEngine.Object.Instantiate(originalClip);
            
            // We perform manipulation using SerializedObject to avoid errors when missing components are present
            SerializedObject serializedObject = new SerializedObject(clip);

            foreach (var prop in serializedObject.ObjectProperties())
            {
                if (prop.name == "path" && prop.propertyType == SerializedPropertyType.String)
                {
                    // Find neighboring classID to determine if this is a Transform reference
                    var classID = prop.FindPropertyRelative("../classID");
                    bool xformMapping = classID != null && classID.intValue == 4;
                    
                    prop.stringValue = MapPath(prop.stringValue, xformMapping);
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (clipCache != null)
            {
                clipCache.Add(originalClip, clip);
            }
            
            return clip;
        }

        public void OnDeactivate(BuildContext context)
        {
            context.AvatarDescriptor.baseAnimationLayers = MapLayers(context.AvatarDescriptor.baseAnimationLayers);
            context.AvatarDescriptor.specialAnimationLayers = MapLayers(context.AvatarDescriptor.specialAnimationLayers);

            foreach (var listener in context.AvatarRootObject.GetComponentsInChildren<IOnCommitObjectRenames>())
            {
                listener.OnCommitObjectRenames(context, this);
            }
        }

        private VRCAvatarDescriptor.CustomAnimLayer[] MapLayers(VRCAvatarDescriptor.CustomAnimLayer[] layers)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                layer.animatorController = ApplyMappingsToAnimator(layer.animatorController);
                layers[i] = layer;
            }

            return layers;
        }
    }
}