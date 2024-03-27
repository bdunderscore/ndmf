#region

using System;
using System.Diagnostics;
using System.Linq;
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

    [NDMFInternal]
    internal class AvatarBuildStateTracker : MonoBehaviour
    {
        internal BuildContext buildContext;
    }

    /// <summary>
    /// This class is the main entry point for triggering NDMF processing of an avatar.
    /// </summary>
    public static class AvatarProcessor
    {
        internal static string TemporaryAssetRoot = "Packages/nadena.dev.ndmf/__Generated";

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

        #if NDMF_VRCSDK3_AVATARS
        private static bool IsVRCFuryHack(System.Diagnostics.StackTrace trace)
        {
            foreach (var frame in trace.GetFrames())
            {
                Debug.Log("Frame: " + frame.GetMethod().DeclaringType.FullName + " " + frame.GetMethod());
            }
            
            return trace.GetFrames().Any(frame =>
                frame.GetMethod().DeclaringType.FullName == "VF.Menu.NdmfFirstMenuItem"
            );
        }
        
        private static bool InHookExecution(System.Diagnostics.StackTrace trace)
        {
            return trace.GetFrames().Any(frame =>
                typeof(VRC.SDKBase.Editor.BuildPipeline.IVRCSDKPreprocessAvatarCallback)
                    .IsAssignableFrom(frame.GetMethod().DeclaringType));
        }
        #else
        private static bool IsVRCFuryHack(System.Diagnostics.StackTrace trace)
        {
            return false;
        }

        private static bool InHookExecution(System.Diagnostics.StackTrace trace)
        {
            return false;
        }
        #endif

        /// <summary>
        /// Processes an avatar as part of an automated process. The resulting assets will be saved in a temporary
        /// location.
        /// </summary>
        /// <param name="root"></param>
        public static void ProcessAvatar(GameObject root)
        {
            ProcessAvatar(root, BuildPhase.Optimizing);
        }
        
        internal static void ProcessAvatar(GameObject root, BuildPhase lastPhase) {
            if (root.GetComponent<AlreadyProcessedTag>()?.processingCompleted == true) return;

            // HACK: VRCFury tries to invoke ProcessAvatar during its own processing, but this risks having Optimization
            // phase passes run too early (before VRCF runs). Detect when we're being invoked like this and skip
            // optimization.
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            if (IsVRCFuryHack(stackTrace))
            {
                if (InHookExecution(stackTrace))
                {
                    Debug.Log("NDMF: Detected VRCFury hack from within VRChat build hooks - " +
                              "ignoring VRCFury invocation");
                    // We're running from within VRChat build hooks, so just ignore VRCFury's request;
                    // we'll be run in the correct order anyway.
                    return;
                }
                else
                {
                    Debug.Log("NDMF: Detected VRCFury hack from play mode - skipping optimization");
                    // Skip optimizations, because they might break VRCFury processing.
                    lastPhase = BuildPhase.Transforming;
                }
            }
            
            var buildContext = new BuildContext(root, TemporaryAssetRoot);

            ProcessAvatar(buildContext, BuildPhase.Resolving, lastPhase);
            buildContext.Finish();

            if (RuntimeUtil.IsPlaying)
            {
                var tag = root.GetComponent<AlreadyProcessedTag>() ?? root.AddComponent<AlreadyProcessedTag>();
                tag.processingCompleted = true;
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