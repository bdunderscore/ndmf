using nadena.dev.ndmf.platform;

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
            if (primaryPlatform != null && primaryPlatform != context.PlatformProvider)
            {
                // Attempt to sync
                var cai = primaryPlatform.ExtractCommonAvatarInfo(context.AvatarRootObject);
                context.PlatformProvider.InitBuildFromCommonAvatarInfo(context, cai);
            }
        }
    }
}