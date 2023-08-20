using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace nadena.dev.build_framework.VRChat
{
    internal class BuildFrameworkPreprocessHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -5000;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                AvatarProcessor.ProcessAvatar(avatarGameObject);
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