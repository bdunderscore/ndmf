namespace nadena.dev.ndmf.VRChatProviders
{
    internal class VRChatBuiltinProviderPlugin : Plugin<VRChatBuiltinProviderPlugin>
    {
        public override string DisplayName => "VRChat SDK";
        public override string QualifiedName => "nadena.dev.ndmf.vrchat.Plugin";

        protected override void Configure()
        {
            // no-op
        }
    }
}