using System;
using System.Diagnostics;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
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

    public static class AvatarProcessor
    {
        internal static string TemporaryAssetRoot = "Packages/nadena.dev.ndmf/__Generated";

        public static void CleanTemporaryAssets()
        {
            AssetDatabase.SaveAssets();

            var subdir = TemporaryAssetRoot;

            AssetDatabase.DeleteAsset(subdir);
            FileUtil.DeleteFileOrDirectory(subdir);
        }

        public static bool CanProcessObject(GameObject avatar)
        {
            return (avatar != null && avatar.GetComponent<VRCAvatarDescriptor>() != null);
        }

        public static GameObject ProcessAvatarUI(GameObject obj)
        {
            using (new OverrideTemporaryDirectoryScope("Assets/ZZZ_GeneratedAssets"))
            {
                var avatar = UnityObject.Instantiate(obj);
                var buildContext = new BuildContext(avatar, AvatarProcessor.TemporaryAssetRoot);

                avatar.transform.position += Vector3.forward * 2f;
                try
                {
                    AssetDatabase.StartAssetEditing();
                    AvatarProcessor.ProcessAvatar(buildContext, BuiltInPhase.Resolving, BuiltInPhase.Optimization);

                    buildContext.Finish();

                    return avatar;
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
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