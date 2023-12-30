#region

using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Engines;
using nadena.dev.ndmf.builtin;
using nadena.dev.ndmf.localization;

#endregion

namespace nadena.dev.ndmf
{
    internal class InternalPasses : Plugin<InternalPasses>
    {
        public override string QualifiedName => "nadena.dev.ndmf.InternalPasses";
        public override string DisplayName => "NDM Framework";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run(RemoveMissingScriptComponents.Instance)
                .Then.Run(RemoveEditorOnlyPass.Instance);
        }
    }
}