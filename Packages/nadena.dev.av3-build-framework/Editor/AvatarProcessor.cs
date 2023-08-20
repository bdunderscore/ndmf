using System;
using System.Diagnostics;
using nadena.dev.build_framework.model;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace nadena.dev.build_framework
{
    using UnityObject = UnityEngine.Object;
    
    public class AvatarProcessor
    {
        [MenuItem("GameObject/[BuildFramework] Manual Bake Avatar", true, 100)]
        static bool ValidateApplyToCurrentAvatarGameobject()
        {
            return true;
        }
        
        [MenuItem("GameObject/[BuildFramework] Manual Bake Avatar", false, 100)]
        static void ApplyToCurrentAvatar()
        {
            var avatar = UnityObject.Instantiate(Selection.activeGameObject);
            var buildContext = new BuildContext(avatar, "Assets/AvatarBuildAssets");

            avatar.transform.position += Vector3.forward * 2f;
            try
            {
                AssetDatabase.StartAssetEditing();
                ProcessAvatar(avatar);
                buildContext.Serialize();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
        
        [MenuItem("Tools/Manual bake plugins")]
        static void ApplyToCurrentAvatar2()
        {
            ApplyToCurrentAvatar();
        }

        public static void ProcessAvatar(GameObject root)
        {
            var buildContext = new BuildContext(root, "Assets/ZZZ_GeneratedAssets");

            ProcessAvatar(buildContext);
        }

        private static void ProcessAvatar(BuildContext buildContext)
        {
            var resolver = new PluginResolver();
            
            foreach (var pass in resolver.Passes)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                try
                {
                    buildContext.RunPass(pass);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Error processing pass " + pass.Description);
                    Debug.LogException(e);
                    throw e;
                }

                Debug.Log($"Processed pass {pass.Description} in {stopwatch.ElapsedMilliseconds} ms");
            }
            
            buildContext.Finish();
        }
    }
}