#region

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf
{
    using UPMClient = UnityEditor.PackageManager.Client;
    using UPM = UnityEditor.PackageManager.Requests;
    #region

    using UnityObject = Object;

    #endregion

    internal class OverrideTemporaryDirectoryScope : IDisposable
    {
        private string priorDirectory = AvatarProcessor.OverrideAssetRoot;

        public OverrideTemporaryDirectoryScope(string path)
        {
            AvatarProcessor.OverrideAssetRoot = path;
        }

        public void Dispose()
        {
            AvatarProcessor.OverrideAssetRoot = priorDirectory;
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
        private static UPM.ListRequest _listRequest;
        
        [InitializeOnLoadMethod]
        static void GetPackageInfo()
        {
            _listRequest = UPMClient.List();
        }

        internal static string DefaultAssetRoot = null;
        internal static string OverrideAssetRoot = null;

        internal static string TemporaryAssetRoot
        {
            get
            {
                if (OverrideAssetRoot != null) return OverrideAssetRoot;
                if (DefaultAssetRoot == null)
                {
                    Stopwatch timer = new Stopwatch();
                    timer.Start();
                    while (!_listRequest.IsCompleted && timer.ElapsedMilliseconds < 10000)
                    {
                        Thread.Sleep(100);
                        // spin-wait - this is important in play mode as the UPM operation won't finish by the time we
                        // need to start processing the avatar.
                    }

                    if (!_listRequest.IsCompleted)
                    {
                        throw new Exception("UPM processing timed out");
                    }

                    var embeddedPkg = _listRequest.Result.FirstOrDefault(pkg =>
                    {
                        if (pkg.name != "nadena.dev.ndmf") return false;
                        return pkg.source == UnityEditor.PackageManager.PackageSource.Embedded;
                    });
                    
                    if (embeddedPkg != null)
                    {
                        DefaultAssetRoot = embeddedPkg.assetPath + "/GeneratedAssets";
                    }
                    else
                    {
                        DefaultAssetRoot = "Assets/ZZZ_GeneratedAssets";
                    }
                    
                    Debug.Log("NDMF: Using path " + DefaultAssetRoot + " for temporary assets");
                }

                return DefaultAssetRoot;
            }
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