using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime.components;

namespace nadena.dev.ndmf.builtin
{
    internal class PrimaryPlatformHolder
    {
        public INDMFPlatformProvider platform;
    }
    
    internal class SyncPlatformConfigPass : Pass<SyncPlatformConfigPass>
    {
        protected override void Execute(BuildContext context)
        {
            var primaryPlatform = PlatformRegistry.GetPrimaryPlatformForAvatar(context.AvatarRootObject);
            context.GetState<PrimaryPlatformHolder>().platform = primaryPlatform;

            var cai = primaryPlatform?.ExtractCommonAvatarInfo(context.AvatarRootObject) ?? new CommonAvatarInfo();
            if (primaryPlatform != GenericPlatform.Instance)
            {
                cai.MergeFrom(GenericPlatform.Instance.ExtractCommonAvatarInfo(context.AvatarRootObject));
            }
            context.PlatformProvider.InitBuildFromCommonAvatarInfo(context, cai);
        }
    }
}