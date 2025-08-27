#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime.components;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.ui.inspector
{
    [CustomEditor(typeof(NDMFAvatarRoot))]
    internal class NDMFAvatarRootEditor : Editor
    {
        private int selectedPlatform = -1;

        private List<KeyValuePair<string, INDMFPlatformProvider>> usablePlatforms;
        private string[] platformDisplayNames;
        
        private void OnEnable()
        {
            usablePlatforms = PlatformRegistry.PlatformProviders
                .Where(kv => kv.Value.HasNativeConfigData)
                .OrderBy(kv => kv.Key)
                .ToList();
            platformDisplayNames = usablePlatforms.Select(kv => kv.Value.DisplayName).ToArray();

            var primaryPlatform = PlatformRegistry.GetPrimaryPlatformForAvatar(((NDMFAvatarRoot)target).gameObject);
            if (primaryPlatform != null)
            {
                selectedPlatform = usablePlatforms.FindIndex(kv => kv.Value == primaryPlatform);
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This is an experimental component. It may change or be removed without notice.",
                MessageType.Warning
            );
            
            // Show heading
            EditorGUILayout.LabelField("Copy settings to/from platform", EditorStyles.boldLabel);
            
            selectedPlatform = EditorGUILayout.Popup("Platform", selectedPlatform, platformDisplayNames);
            EditorGUILayout.BeginHorizontal();

            INDMFPlatformProvider? platform = selectedPlatform >= 0 && selectedPlatform < usablePlatforms.Count
                ? usablePlatforms[selectedPlatform].Value
                : null;
            
            using (var _d = new EditorGUI.DisabledScope(platform == GenericPlatform.Instance || platform == null))
            {
                var avatarRoot = ((NDMFAvatarRoot)target).gameObject;
                var cai = GenericPlatform.Instance.ExtractCommonAvatarInfo(avatarRoot);
                
                using (var _d2 = new EditorGUI.DisabledScope(platform?.CanInitFromCommonAvatarInfo(avatarRoot, cai) != true))
                {
                    if (GUILayout.Button("Copy to platform"))
                    {
                        CopyTo(usablePlatforms[selectedPlatform].Value);
                    }
                }

                if (GUILayout.Button("Copy from platform"))
                {
                    CopyFrom(usablePlatforms[selectedPlatform].Value);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CopyTo(INDMFPlatformProvider provider)
        {
            CopyFromTo(GenericPlatform.Instance, provider);
        }

        private void CopyFrom(INDMFPlatformProvider provider)
        {
            CopyFromTo(provider, GenericPlatform.Instance);
        }

        private void CopyFromTo(INDMFPlatformProvider source, INDMFPlatformProvider dest)
        {
            var avatarRoot = ((NDMFAvatarRoot)target).gameObject;
            
            Undo.RegisterFullObjectHierarchyUndo(avatarRoot, "Copy avatar platform configuration");

            var cai = source.ExtractCommonAvatarInfo(avatarRoot);
            dest.InitFromCommonAvatarInfo(avatarRoot, cai);
            
            // Find any prefabs rooted at the avatar root or below
            var prefabRoots = new HashSet<GameObject>();
            foreach (var transform in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(transform);
                if (root != null)
                {
                    prefabRoots.Add(root);
                }
            }

            foreach (var root in prefabRoots)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            }
        }
    }
}