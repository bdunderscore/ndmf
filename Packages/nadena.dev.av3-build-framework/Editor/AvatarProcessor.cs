using System;
using System.Diagnostics;
using nadena.dev.build_framework.model;
using nadena.dev.build_framework.runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace nadena.dev.build_framework
{
    using UnityObject = UnityEngine.Object;

    internal class OverrideTemporaryDirectoryScope : IDisposable
    {
        private string priorDirectory = AvatarProcessor.TemporaryAssetRoot;
        public OverrideTemporaryDirectoryScope(string path)
        {
            AvatarProcessor.TemporaryAssetRoot = path;
        }
        
        public void Dispose()
        {
            AvatarProcessor.TemporaryAssetRoot = priorDirectory;
        }
    }
    
    public class AvatarProcessor
    {
        internal static string TemporaryAssetRoot = "Packages/nadena.dev.av3-build-framework/__Generated";
        
        [MenuItem("GameObject/[BuildFramework] Manual Bake Avatar", true, 100)]
        static bool ValidateApplyToCurrentAvatarGameobject()
        {
            return true;
        }
        
        [MenuItem("GameObject/[BuildFramework] Manual Bake Avatar", false, 100)]
        static void ApplyToCurrentAvatar()
        {
            using (new OverrideTemporaryDirectoryScope("Assets/ZZZ_GeneratedAssets"))
            {
                var avatar = UnityObject.Instantiate(Selection.activeGameObject);
                var buildContext = new BuildContext(avatar, TemporaryAssetRoot);

                avatar.transform.position += Vector3.forward * 2f;
                try
                {
                    AssetDatabase.StartAssetEditing();
                    ProcessAvatar(buildContext);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
        }
        
        [MenuItem("Tools/Manual bake plugins")]
        static void ApplyToCurrentAvatar2()
        {
            ApplyToCurrentAvatar();
        }

        public static void CleanTemporaryAssets()
        {
            AssetDatabase.SaveAssets();

            var subdir = TemporaryAssetRoot;

            AssetDatabase.DeleteAsset(subdir);
            FileUtil.DeleteFileOrDirectory(subdir);
        }
        
        public static void ProcessAvatar(GameObject root)
        {
            if (root.GetComponent<AlreadyProcessedTag>()) return;
            
            var buildContext = new BuildContext(root, "Assets/ZZZ_GeneratedAssets");

            ProcessAvatar(buildContext);

            if (RuntimeUtil.isPlaying) root.AddComponent<AlreadyProcessedTag>();
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