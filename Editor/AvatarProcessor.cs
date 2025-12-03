#region

using System;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if NDMF_VRCSDK3_AVATARS
using VRC.SDKBase.Editor.BuildPipeline;
#endif

#endregion

namespace nadena.dev.ndmf
{
    #region

    using UnityObject = Object;

    #endregion

    /// <summary>
    /// Temporarily overrides the directory where NDMF will save temporary assets.
    /// </summary>
    public class OverrideTemporaryDirectoryScope : IDisposable
    {
        private string priorDirectory = AvatarProcessor.TemporaryAssetRoot;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The directory to use; pass null to indicate that saving assets is not required (useful
        /// for unit tests, or for platforms which do not generate asset bundles)</param>
        public OverrideTemporaryDirectoryScope([CanBeNull] string path)
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
        /// Event that is invoked when an avatar is manually processed.
        /// </summary>
        public static event Action<GameObject, INDMFPlatformProvider> OnManualProcessAvatar;

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

        [Obsolete("ProcessAvatarUI() does not handle platforms correctly. Please use ManualProcessAvatar() instead, and specify VRChatPlatform to retain the behavior of ProcessAvatarUI().")]
        public static GameObject ProcessAvatarUI(GameObject obj)
        {
            return ManualProcessAvatar(obj, AmbientPlatform.DefaultPlatform);
        }

        /// <summary>
        /// Process an avatar on request by the user. The resulting assets will be saved in a persistent directory
        /// that will not be cleaned up by CleanTemporaryAssets.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        [PublicAPI]
        public static GameObject ManualProcessAvatar(GameObject obj, INDMFPlatformProvider platform = null)
        {
            using (new OverrideTemporaryDirectoryScope("Assets/ZZZ_GeneratedAssets"))
            {
                var avatar = UnityObject.Instantiate(obj);
                platform ??= AmbientPlatform.CurrentPlatform;
                var buildContext = new BuildContext(avatar, TemporaryAssetRoot, platform);

                avatar.transform.position += Vector3.forward * 2f;
                try
                {
                    AssetDatabase.StartAssetEditing();
                    ProcessAvatar(buildContext, BuildPhase.BuiltInPhases.First(), BuildPhase.BuiltInPhases.Last());

                    buildContext.Finish();

                    OnManualProcessAvatar?.Invoke(avatar, platform);
                    return avatar;
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
        }

        #if NDMF_VRCSDK3_AVATARS
        private static bool IsVRCFuryHack(StackTrace trace)
        {
            return trace.GetFrames().Any(frame =>
                frame.GetMethod().DeclaringType.FullName == "VF.Menu.NdmfFirstMenuItem"
            );
        }

        private static bool InHookExecution(StackTrace trace)
        {
            return trace.GetFrames().Any(frame =>
                typeof(IVRCSDKPreprocessAvatarCallback)
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
            ProcessAvatar(root, BuildPhase.Last);
        }

        /// <summary>
        /// Processes an avatar as part of an automated process, using a specific platform provider.
        /// The platform provider will be set as the ambient platform provider for the duration of the
        /// build.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [PublicAPI]
        public static BuildContext ProcessAvatar(
            GameObject root,
            INDMFPlatformProvider platform
        )
        {
            using var scope = new AmbientPlatform.Scope(platform);
            
            var context = ProcessAvatar(root, BuildPhase.Last);
            if (context == null)
            {
                // TODO - does this break VRCF?
                throw new Exception("Avatar already processed");
            }

            return context;
        }
        
        [CanBeNull]
        internal static BuildContext ProcessAvatar(GameObject root, BuildPhase lastPhase) {
            if (root.GetComponent<AlreadyProcessedTag>()?.processingCompleted == true) return null;

            // HACK: VRCFury tries to invoke ProcessAvatar during its own processing, but this risks having Optimization
            // phase passes run too early (before VRCF runs). Detect when we're being invoked like this and skip
            // optimization.
            var stackTrace = new StackTrace();
            if (IsVRCFuryHack(stackTrace))
            {
                if (InHookExecution(stackTrace))
                {
                    Debug.Log("NDMF: Detected VRCFury hack from within VRChat build hooks - " +
                              "ignoring VRCFury invocation");
                    // We're running from within VRChat build hooks, so just ignore VRCFury's request;
                    // we'll be run in the correct order anyway.
                    return null;
                }
                else
                {
                    Debug.Log("NDMF: Detected VRCFury hack from play mode - skipping optimization");
                    // Skip optimizations, because they might break VRCFury processing.
                    lastPhase = BuildPhase.Transforming;
                }
            }
            
            var buildContext = new BuildContext(root, TemporaryAssetRoot, AmbientPlatform.CurrentPlatform);

            ProcessAvatar(buildContext, BuildPhase.First, lastPhase);
            buildContext.Finish();

            if (RuntimeUtil.IsPlaying)
            {
                var tag = root.GetComponent<AlreadyProcessedTag>() ?? root.AddComponent<AlreadyProcessedTag>();
                tag.processingCompleted = true;
            }

            return buildContext;
        }

        internal static void ProcessAvatar(BuildContext buildContext, BuildPhase firstPhase, BuildPhase lastPhase)
        {
            using var _platformScope = new AmbientPlatform.Scope(buildContext.PlatformProvider);
            
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
                        throw;
                    }

                    stopwatch.Stop();

                    Debug.Log($"Processed pass {pass.Description} in {stopwatch.ElapsedMilliseconds} ms");
                }

                if (lastPhase == phase) break;
            }
        }
    }
}