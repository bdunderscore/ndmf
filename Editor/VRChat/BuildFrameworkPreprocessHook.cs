#if NDMF_VRCSDK3_AVATARS

#region

using System;
using System.Diagnostics;
using nadena.dev.ndmf.config;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.VRChat
{
    [AddComponentMenu("")] [NDMFInternal]
    internal class ContextHolder : MonoBehaviour
    {
        internal BuildContext context;
    }

    internal class BuildFrameworkPreprocessHook : IVRCSDKPreprocessAvatarCallback
    {
        // Must run before -10000 (VRCFury)
        public int callbackOrder => -11000;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var isPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
            var shouldApply = isPlayMode ? Config.ApplyOnPlay : Config.ApplyOnBuild;
            if (!shouldApply) return true;
            
            // Legacy: For VRCF
            if (avatarGameObject.GetComponent<AlreadyProcessedTag>()?.processingCompleted == true) return true;

            var state = HookDedup.RecordAvatar(avatarGameObject);
            if (state.ranEarlyHook) return true;
            state.ranEarlyHook = true;

            try
            {
                var holder = avatarGameObject.AddComponent<ContextHolder>();
                holder.hideFlags = HideFlags.DontSave;
                holder.context = new BuildContext(avatarGameObject, AvatarProcessor.TemporaryAssetRoot);

                AvatarProcessor.ProcessAvatar(holder.context, BuildPhase.Resolving, BuildPhase.Transforming);
                holder.context.Serialize();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }

    internal class BuildFrameworkOptimizeHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -1025; // just before RemoveAvatarEditorOnly

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var isPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
            var shouldApply = isPlayMode ? Config.ApplyOnPlay : Config.ApplyOnBuild;
            if (!shouldApply) return true;
            
            if (avatarGameObject.GetComponent<AlreadyProcessedTag>()?.processingCompleted == true) return true;
            
            var state = HookDedup.RecordAvatar(avatarGameObject);
            if (state.ranOptimization) return true;
            state.ranOptimization = true;
            
            var holder = avatarGameObject.GetComponent<ContextHolder>();
            if (holder == null) return true;

            try
            {
                AvatarProcessor.ProcessAvatar(holder.context, BuildPhase.Optimizing, BuildPhase.Optimizing);
                Stopwatch sw = new Stopwatch();
                sw.Start();
                holder.context.Finish();
                sw.Stop();
                Debug.Log($"Build Framework: Saved assets in {sw.ElapsedMilliseconds}ms");
                Object.DestroyImmediate(holder);

                return holder.context.Successful;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}

#endif
