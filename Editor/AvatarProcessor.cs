using System;
using System.Diagnostics;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace nadena.dev.ndmf
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

    internal class AvatarBuildStateTracker : MonoBehaviour
    {
        internal BuildContext buildContext;
    }

    public class AvatarProcessor
    {
        internal static string TemporaryAssetRoot = "Packages/nadena.dev.ndmf/__Generated";

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
                    ProcessAvatar(buildContext, BuiltInPhase.Resolving, BuiltInPhase.Optimization);

                    buildContext.Finish();
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

            var buildContext = new BuildContext(root, TemporaryAssetRoot);

            ProcessAvatar(buildContext, BuiltInPhase.Resolving, BuiltInPhase.Optimization);
            buildContext.Finish();

            if (RuntimeUtil.isPlaying)
            {
                root.AddComponent<AlreadyProcessedTag>();
            }
        }

        internal static void ProcessAvatar(BuildContext buildContext, BuiltInPhase firstPhase, BuiltInPhase lastPhase)
        {
            var resolver = new PluginResolver();

            for (var phase = firstPhase; phase <= lastPhase; phase++)
            {
                Debug.Log($"=== Processing phase {phase} ===");
                if (!resolver.Passes.TryGetValue(phase, out var passes))
                {
                    continue;
                }

                foreach (var pass in passes)
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

                    stopwatch.Stop();

                    Debug.Log($"Processed pass {pass.Description} in {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }
    }
}