using nadena.dev.ndmf.platform;

namespace nadena.dev.ndmf.builtin
{
    public class SyncPlatformConfigPass : Pass<SyncPlatformConfigPass>
    {
        protected override void Execute(BuildContext context)
        {
            var primaryPlatform = PlatformRegistry.GetPrimaryPlatformForAvatar(context.AvatarRootObject);
            if (primaryPlatform != null && primaryPlatform != context.PlatformProvider)
            {
                // Attempt to sync
                var cai = primaryPlatform.ExtractCommonAvatarInfo(context.AvatarRootObject);
                context.PlatformProvider.InitBuildFromCommonAvatarInfo(context, cai);
            }
        }
    }
}