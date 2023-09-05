using System.Collections.Immutable;

namespace nadena.dev.ndmf
{
    internal class InternalPasses : Plugin
    {
        public override string QualifiedName => "nadena.dev.ndmf.InternalPasses";

        public override ImmutableList<PluginPass> Passes => ImmutableList<PluginPass>.Empty
            .Add(RemoveEditorOnly.Instance);
    }
}