using System.Collections.Immutable;

namespace nadena.dev.ndmf
{
    public class BuildPhase
    {
        public string Name { get; }
        // Prevent extension for now
        internal BuildPhase(string name)
        {
            Name = name;
        }

        public static readonly BuildPhase Resolving = new BuildPhase("Resolving");
        public static readonly BuildPhase Generating = new BuildPhase("Generating");
        public static readonly BuildPhase Transforming = new BuildPhase("Transforming");
        public static readonly BuildPhase Optimizing = new BuildPhase("Optimizing");
        
        public static readonly ImmutableList<BuildPhase> BuiltInPhases
            = ImmutableList.Create(Resolving, Generating, Transforming, Optimizing);

        public override string ToString() => Name;
    }
}