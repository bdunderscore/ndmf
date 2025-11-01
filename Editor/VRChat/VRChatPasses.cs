using nadena.dev.ndmf;
using nadena.dev.ndmf.VRChat;

[assembly: ExportsPlugin(typeof(VRChatPasses))]

namespace nadena.dev.ndmf.VRChat
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class VRChatPasses : Plugin<VRChatPasses>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.PlatformFinish).Run(CheckMipStreamingPass.Instance);
        }
    }
}