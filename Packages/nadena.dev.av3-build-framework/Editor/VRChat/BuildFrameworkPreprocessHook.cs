using System;
using System.Diagnostics;
using nadena.dev.build_framework.runtime;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;

namespace nadena.dev.build_framework.VRChat
{
    [AddComponentMenu("")]
    internal class ContextHolder : MonoBehaviour
    {
        internal BuildContext context;
    }
    
    internal class BuildFrameworkPreprocessHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -5000;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (avatarGameObject.GetComponent<AlreadyProcessedTag>()) return true;
            
            try
            {
                var holder = avatarGameObject.AddComponent<ContextHolder>();
                holder.hideFlags = HideFlags.DontSave;
                holder.context = new BuildContext(avatarGameObject, AvatarProcessor.TemporaryAssetRoot);
                
                AvatarProcessor.ProcessAvatar(holder.context, BuiltInPhase.Resolving, BuiltInPhase.Transforming);
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
        public int callbackOrder => 0;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (avatarGameObject.GetComponent<AlreadyProcessedTag>()) return true;

            var holder = avatarGameObject.GetComponent<ContextHolder>();
            if (holder == null) return true;
            
            try
            {
                AvatarProcessor.ProcessAvatar(holder.context, BuiltInPhase.Optimization, BuiltInPhase.Optimization);
                Stopwatch sw = new Stopwatch();
                sw.Start();
                holder.context.Finish();
                sw.Stop();
                Debug.Log($"Build Framework: Saved assets in {sw.ElapsedMilliseconds}ms");
                UnityEngine.Object.DestroyImmediate(holder);
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}