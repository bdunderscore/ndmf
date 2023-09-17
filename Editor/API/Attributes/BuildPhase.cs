#region

using System.Collections.Immutable;

#endregion

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Build Phases provide a coarse mechanism for grouping passes for execution. Each build phase has a recommended
    /// usage to help avoid ordering conflicts without needing explicit constraints.
    ///
    /// Currently, the following phases are defined:
    /// - Resolving
    /// - Generating
    /// - Transforming
    /// - Optimizing
    /// </summary>
    public sealed class BuildPhase
    {
        public string Name { get; }

        // Prevent extension for now
        internal BuildPhase(string name)
        {
            Name = name;
        }

        /// <summary>
        /// The resolving phase is intended for use by passes which perform very early processing of components and
        /// avatar state, before any large-scale changes have been made. For example, Modular Avatar uses this phase
        /// to resolve string-serialized object passes to their destinations, and to clone animation controllers before
        /// any changes are made to them.
        ///
        /// NDMF also has a built-in phase in Resolving, which removes EditorOnly objects. For more information,
        /// see nadena.dev.ndmf.builtin.RemoveEditorOnlyPass.
        /// </summary>
        /// <see cref="nadena.dev.ndmf.builtin.RemoveEditorOnlyPass"/>
        public static readonly BuildPhase Resolving = new BuildPhase("Resolving");

        /// <summary>
        /// The generating phase is intended for use by asses which generate components used by later plugins. For
        /// example, if you want to generate components that will be used by Modular Avatar, this would be the place
        /// to do it.
        /// </summary>
        public static readonly BuildPhase Generating = new BuildPhase("Generating");

        /// <summary>
        /// The transforming phase is intended for general-purpose avatar transformations. Most of Modular Avatar's
        /// logic runs here.
        /// </summary>
        public static readonly BuildPhase Transforming = new BuildPhase("Transforming");

        /// <summary>
        /// The optimizing phase is intended for pure optimizations that need to run late in the build process.
        /// </summary>
        public static readonly BuildPhase Optimizing = new BuildPhase("Optimizing");

        /// <summary>
        /// This list contains all built-in phases in the order that they will be executed.
        /// </summary>
        public static readonly ImmutableList<BuildPhase> BuiltInPhases
            = ImmutableList.Create(Resolving, Generating, Transforming, Optimizing);

        public override string ToString() => Name;
    }
}