#region

using System;
using System.Diagnostics;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf
{
    #region

    using UnityObject = Object;

    #endregion

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

    /// <summary>
    /// This class is the main entry point for triggering NDMF processing of an avatar.
    /// </summary>
    public static class AvatarProcessor
    {
        internal static string asset = "Packages/nadena.dev.ndmf/__Generated";
        internal static string key = "nadena.dev.ndmf.path";

        internal static string TemporaryAssetRoot
        {
            set
            {
                EditorPrefs.SetString(key, value);
                Debug.Log($"Temporary Directory : Change Into \"{value}\"");
            }
            get => EditorPrefs.GetString(key);
        }

        [MenuItem("Tools/NDM Framework/Temporary Directory/Set")]
        internal static void SetTemporaryDirectory()
        {
            string curPath = (System.IO.Directory.Exists($"{Application.dataPath}/../{TemporaryAssetRoot}")) ? $"{Application.dataPath}/../{TemporaryAssetRoot}" : Application.dataPath;
            string newPath = EditorUtility.SaveFolderPanel("Set Temporary Directory", curPath.Substring(Application.dataPath.Length - "Assets".Length), string.Empty);

            if (!newPath.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Temporary Path Error", "Please select a directory in Assets.", "OK");
            }
            else
            {
                TemporaryAssetRoot = newPath.Substring(Application.dataPath.Length - "Assets".Length);
            }
        }

        [MenuItem("Tools/NDM Framework/Temporary Directory/Reset")]
        internal static void ResetTemporaryDirectory()
        {
            TemporaryAssetRoot = asset;
        }

        /// <summary>
        /// Deletes all temporary assets after a build.
        /// </summary>
        public static void CleanTemporaryAssets()
        {
            AssetDatabase.SaveAssets();

            var subdir = TemporaryAssetRoot;

            AssetDatabase.DeleteAsset(subdir);
            FileUtil.DeleteFileOrDirectory(subdir);
        }

        /// <summary>
        /// Returns true if the given GameObject appears to be an avatar that can be processed.
        /// </summary>
        /// <param name="avatar"></param>
        /// <returns></returns>
        public static bool CanProcessObject(GameObject avatar)
        {
            return PlatformExtensions.CanProcessObject(avatar);
        }

        /// <summary>
        /// Process an avatar on request by the user. The resulting assets will be saved in a persistent directory
        /// that will not be cleaned up by CleanTemporaryAssets.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static GameObject ProcessAvatarUI(GameObject obj)
        {
            using (new OverrideTemporaryDirectoryScope("Assets/ZZZ_GeneratedAssets"))
            {
                var avatar = UnityObject.Instantiate(obj);
                var buildContext = new BuildContext(avatar, TemporaryAssetRoot);

                avatar.transform.position += Vector3.forward * 2f;
                try
                {
                    AssetDatabase.StartAssetEditing();
                    ProcessAvatar(buildContext, BuildPhase.Resolving, BuildPhase.Optimizing);

                    buildContext.Finish();

                    return avatar;
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
        }

        /// <summary>
        /// Processes an avatar as part of an automated process. The resulting assets will be saved in a temporary
        /// location.
        /// </summary>
        /// <param name="root"></param>
        public static void ProcessAvatar(GameObject root)
        {
            if (root.GetComponent<AlreadyProcessedTag>()) return;

            var buildContext = new BuildContext(root, TemporaryAssetRoot);

            ProcessAvatar(buildContext, BuildPhase.Resolving, BuildPhase.Optimizing);
            buildContext.Finish();

            if (RuntimeUtil.IsPlaying)
            {
                root.AddComponent<AlreadyProcessedTag>();
            }
        }

        internal static void ProcessAvatar(BuildContext buildContext, BuildPhase firstPhase, BuildPhase lastPhase)
        {
            var resolver = new PluginResolver();
            bool processing = false;

            foreach (var (phase, passes) in resolver.Passes)
            {
                if (firstPhase == phase) processing = true;
                if (!processing) continue;

                Debug.Log($"=== Processing phase {phase} ===");

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
                        Debug.LogError("Error processing pass " + pass.Description);
                        Debug.LogException(e);
                        throw e;
                    }

                    stopwatch.Stop();

                    Debug.Log($"Processed pass {pass.Description} in {stopwatch.ElapsedMilliseconds} ms");
                }

                if (lastPhase == phase) break;
            }
        }
    }
}