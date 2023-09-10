using nadena.dev.ndmf.builtin;
using nadena.dev.ndmf.fluent;

namespace nadena.dev.ndmf
{
    internal class InternalPasses : Plugin<InternalPasses>
    {
        public override string QualifiedName => "nadena.dev.ndmf.InternalPasses";
        public override string DisplayName => "NDM Framework";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run(RemoveEditorOnlyPass.Instance);
        }
    }
}