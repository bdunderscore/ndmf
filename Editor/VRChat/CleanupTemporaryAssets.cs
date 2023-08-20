using VRC.SDKBase.Editor.BuildPipeline;

namespace nadena.dev.build_framework.VRChat
{
    public class CleanupTemporaryAssets : IVRCSDKPostprocessAvatarCallback
    {
        public int callbackOrder => 0;
        public void OnPostprocessAvatar()
        {
            //AvatarProcessor.CleanTemporaryAssets();
        }
    }
}