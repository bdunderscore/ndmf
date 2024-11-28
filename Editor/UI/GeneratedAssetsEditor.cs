using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.ui
{
    [CustomEditor(typeof(GeneratedAssets))]
    class MAAssetBundleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Unpack"))
            {
                foreach (var target in targets)
                {
                    GeneratedAssets bundle = (GeneratedAssets) target;
                    bundle.Extract();
                }
            }
        }
    }

    public static class GeneratedAssetBundleExtension
    {
        /// <summary>
        /// Extracts a generated assets bundle into separate asset files
        /// </summary>
        /// <param name="bundle"></param>
        public static void Extract(this GeneratedAssets bundle)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                new GeneratedAssetBundleExtractor(bundle).Extract();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
    
    internal class GeneratedAssetBundleExtractor
    {
        private static readonly ISet<Type> RootAssets = new HashSet<Type>()
        {
            typeof(Mesh),
            typeof(AnimationClip),
            typeof(RuntimeAnimatorController),
#if NDMF_VRCSDK3_AVATARS
            typeof(VRCExpressionParameters),
            typeof(VRCExpressionsMenu),
#endif
        };

        private static readonly ISet<Type> HiddenAssets = new HashSet<Type>()
        {
            typeof(AnimatorState),
            typeof(AnimatorStateMachine),
            typeof(AnimatorTransitionBase),
            typeof(BlendTree),
            typeof(StateMachineBehaviour),
        };

        private static Dictionary<Type, bool> ShouldHideAssetCache = new Dictionary<Type, bool>();
        
        internal static bool IsAssetTypeHidden(Type t)
        {
            // This is to keep project view clean

            if (ShouldHideAssetCache.TryGetValue(t, out var hide)) return hide;
            hide = HiddenAssets.Any(potentialBase => potentialBase.IsAssignableFrom(t));
            ShouldHideAssetCache[t] = hide;

            return hide;
        }

        private Dictionary<UnityEngine.Object, AssetInfo> _assets;
        private GeneratedAssets Bundle;
        private HashSet<Object> _unassigned;
        
        internal GeneratedAssetBundleExtractor(GeneratedAssets bundle)
        {
            _assets = GetContainedAssets(bundle);
            this.Bundle = bundle;
        }

        class AssetInfo
        {
            public readonly UnityEngine.Object Asset;
            public readonly HashSet<AssetInfo> IncomingReferences = new HashSet<AssetInfo>();
            public readonly Dictionary<AssetInfo, List<string>> OutgoingReferences = new();

            public AssetInfo Root;

            public AssetInfo(UnityEngine.Object obj)
            {
                this.Asset = obj;
            }

            public void PopulateReferences(Dictionary<UnityEngine.Object, AssetInfo> assets)
            {
                switch (Asset)
                {
                    case Mesh _:
                    case AnimationClip _:
#if NDMF_VRCSDK3_AVATARS
                    case VRCExpressionParameters _:
                    case VRCExpressionsMenu _:
#endif
                        return; // No child objects
                }

                var so = new SerializedObject(Asset);
                var prop = so.GetIterator();

                // TODO extract to common code
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = true;
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var value = prop.objectReferenceValue;
                        if (value != null && assets.TryGetValue(value, out var target))
                        {
                            if (!OutgoingReferences.TryGetValue(target, out var fixups))
                            {
                                fixups = new();
                                OutgoingReferences[target] = fixups;
                            }
                            fixups.Add(prop.propertyPath);
                            target.IncomingReferences.Add(this);
                        }
                    }
                    else if (prop.propertyType == SerializedPropertyType.String)
                    {
                        enterChildren = false;
                    }
                }
            }

            public void ApplyFixups()
            {
                if (OutgoingReferences.Count == 0) return;
                
                var so = new SerializedObject(Asset);
                
                foreach (var (target, fixups) in OutgoingReferences)
                {
                    foreach (var fixup in fixups)
                    {
                        var prop = so.FindProperty(fixup);
                        if (prop == null)
                        {
                            Debug.LogWarning($"Failed to find property {fixup} on {Asset.name}");
                            continue;
                        }

                        prop.objectReferenceValue = target.Asset;
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            public void ForceAssignRoot()
            {
                // First, see if we're reachable only from one root.
                HashSet<AssetInfo> visited = new HashSet<AssetInfo>();
                HashSet<AssetInfo> roots = new HashSet<AssetInfo>();
                Queue<AssetInfo> queue = new Queue<AssetInfo>();
                visited.Add(this);
                queue.Enqueue(this);

                while (queue.Count > 0 && roots.Count < 2)
                {
                    var next = queue.Dequeue();
                    if (next.Root != null)
                    {
                        roots.Add(next.Root);
                    }

                    foreach (var outgoingReference in next.IncomingReferences)
                    {
                        if (visited.Add(outgoingReference))
                        {
                            queue.Enqueue(outgoingReference);
                        }
                    }
                }

                if (roots.Count == 1)
                {
                    this.Root = roots.First();
                }
                else
                {
                    this.Root = this;
                }
            }
        }

        public static void Unpack(GeneratedAssets bundle)
        {
            new GeneratedAssetBundleExtractor(bundle).Extract();
        }


        private bool TryAssignRoot(AssetInfo info)
        {
            if (info.Root != null)
            {
                return true;
            }

            if (RootAssets.Any(t => t.IsInstanceOfType(info.Asset)) || info.IncomingReferences.Count == 0)
            {
                info.Root = info;
                return true;
            }

            var firstRoot = info.IncomingReferences.First().Root;
            if (firstRoot != null && !_unassigned.Contains(firstRoot.Asset)
                                  && info.IncomingReferences.All(t => t.Root == firstRoot))
            {
                info.Root = firstRoot;
                return true;
            }

            return false;
        }

        internal void Extract()
        {
            string path = AssetDatabase.GetAssetPath(Bundle);

            var directory = System.IO.Path.GetDirectoryName(path);
            _unassigned = new HashSet<UnityEngine.Object>(_assets.Keys);

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var info in _assets.Values)
                {
                    info.PopulateReferences(_assets);
                }

                var queue = new Queue<UnityEngine.Object>();
                while (_unassigned.Count > 0)
                {
                    // Bootstrap
                    if (queue.Count == 0)
                    {
                        _unassigned.Where(o => TryAssignRoot(_assets[o])).ToList().ForEach(o => { queue.Enqueue(o); });

                        if (queue.Count == 0)
                        {
                            _assets[_unassigned.First()].ForceAssignRoot();
                            queue.Enqueue(_unassigned.First());
                        }
                    }

                    while (queue.Count > 0)
                    {
                        var next = queue.Dequeue();
                        ProcessSingleAsset(directory, next);
                        _unassigned.Remove(next);

                        foreach (var outgoingReference in _assets[next].OutgoingReferences.Keys)
                        {
                            if (_unassigned.Contains(outgoingReference.Asset) && TryAssignRoot(outgoingReference))
                            {
                                queue.Enqueue(outgoingReference.Asset);
                            }
                        }
                    }
                }
                
                // The above movements can break some inter-asset references. Fix them now before we save assets.
                foreach (var asset in _assets.Values)
                {
                    asset.ApplyFixups();                
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }
            
            foreach (var subcontainer in Bundle.SubAssets)
            {
                var remaining = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(subcontainer))
                    .Where(asset => asset != subcontainer).ToList();
                if (remaining.Count > 0) Debug.Log($"Failed to extract: " + string.Join(", ", remaining.Select(o => o.name + " " + o.GetType().Name)));
                
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(subcontainer));
            }

            AssetDatabase.DeleteAsset(path);
        }

        private string AssignAssetFilename(string directory, Object next)
        {
            string assetName = next.name;
            if (string.IsNullOrEmpty(assetName))
            {
                next.name = next.GetType().Name + " " + GUID.Generate().ToString();
                assetName = next.name;
            }

            string assetFile;
            for (int extension = 0;; extension++)
            {
                assetFile = assetName + (extension == 0 ? "" : $" ({extension})") + ".asset";
                assetFile = System.IO.Path.Combine(directory, assetFile);
                if (!System.IO.File.Exists(assetFile))
                {
                    break;
                }
            }

            return assetFile;
        }

        private void ProcessSingleAsset(string directory, Object next)
        {
            AssetDatabase.RemoveObjectFromAsset(next);

            var info = _assets[next];
            if (info.Root != info)
            {
                if (!AssetDatabase.IsMainAsset(info.Root.Asset))
                {
                    throw new Exception(
                        $"Desired root {info.Root.Asset.name} for asset {next.name} is not a root asset");
                }

                if (IsAssetTypeHidden(next.GetType()))
                {
                    next.hideFlags |= HideFlags.HideInHierarchy;
                }

                AssetDatabase.AddObjectToAsset(next, info.Root.Asset);
            }
            else
            {
                next.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(next, AssignAssetFilename(directory, next));
            }
        }

        private static Dictionary<Object, AssetInfo> GetContainedAssets(GeneratedAssets bundle)
        {
            string path = AssetDatabase.GetAssetPath(bundle);
            var rawAssets = new List<UnityEngine.Object>(AssetDatabase.LoadAllAssetsAtPath(path));

            foreach (var subcontainer in bundle.SubAssets)
            {
                rawAssets.AddRange(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(subcontainer)));
            }
            
            Dictionary<Object, AssetInfo> infos = new Dictionary<Object, AssetInfo>(rawAssets.Count);
            foreach (var asset in rawAssets)
            {
                if (!(asset is GeneratedAssets or SubAssetContainer))
                {
                    infos.Add(asset, new AssetInfo(asset));
                }
            }
            
            return infos;
        }
    }
}