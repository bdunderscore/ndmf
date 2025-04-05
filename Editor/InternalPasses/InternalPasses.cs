#region

using nadena.dev.ndmf.builtin;
using nadena.dev.ndmf.runtime;

#endregion

namespace nadena.dev.ndmf
{
    [NDMFInternal]
    [RunsOnAllPlatforms]
    internal class InternalPasses : Plugin<InternalPasses>
    {
        public override string QualifiedName => "nadena.dev.ndmf.InternalPasses";
        public override string DisplayName => "NDM Framework";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run(RemoveMissingScriptComponents.Instance)
                .Then.Run(RemoveEditorOnlyPass.Instance)
                .Then
                .OnPlatforms(new[] { "ndmf/nonexistent" },
                    seq => { seq.Run("TEST - Should not run (incompatible platform)", _ => { }); });
        }
    }
}