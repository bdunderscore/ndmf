#if NDMF_VRCSDK3_AVATARS

#region

using VRC.SDKBase.Editor.BuildPipeline;

#endregion

namespace nadena.dev.ndmf.VRChat
{
    internal class CleanupTemporaryAssets : IVRCSDKPostprocessAvatarCallback
    {
        public int callbackOrder => 0;

        public void OnPostprocessAvatar()
        {
            AvatarProcessor.CleanTemporaryAssets();
        }
    }
}

#endif
