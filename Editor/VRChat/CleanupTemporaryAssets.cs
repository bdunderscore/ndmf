using VRC.SDKBase.Editor.BuildPipeline;

namespace nadena.dev.ndmf.VRChat
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