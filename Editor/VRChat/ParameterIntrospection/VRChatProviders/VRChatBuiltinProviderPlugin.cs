using UnityEngine;

namespace nadena.dev.ndmf.VRChatProviders
{
    internal class VRChatBuiltinProviderPlugin : Plugin<VRChatBuiltinProviderPlugin>
    {
        public override string DisplayName => "VRChat SDK";
        public override string QualifiedName => "nadena.dev.ndmf.vrchat.Plugin";
        // #8143e6
        public override Color? ThemeColor => new Color(0x81 / 255f, 0x43 / 255f, 0xe6 / 255f, 1);

        protected override void Configure()
        {
            // no-op
        }
    }
}