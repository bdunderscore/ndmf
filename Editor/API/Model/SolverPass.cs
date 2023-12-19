#region

using System;
using System.Collections.Immutable;

#endregion

namespace nadena.dev.ndmf.model
{
    /// <summary>
    /// Internal class representing metadata about a pass. Used as part of internal solver operations and as part of
    /// pass execution.
    /// </summary>
    internal class SolverPass
    {
        internal IPass Pass { get; }
        internal BuildPhase Phase { get; }

        internal IPluginInternal Plugin { get; }
        internal PassKey PassKey => Pass.PassKey;
        internal bool IsPhantom => Pass.IsPhantom;

        internal IImmutableSet<Type> RequiredExtensions { get; set; }
        internal IImmutableSet<string> CompatibleExtensions { get; set; }

        internal bool IsExtensionCompatible(Type ty)
        {
            return IsPhantom || RequiredExtensions.Contains(ty) || CompatibleExtensions.Contains(ty.FullName);
        }

        internal SolverPass(IPluginInternal plugin, IPass pass, BuildPhase phase, IImmutableSet<string> compatibleExtensions,
            IImmutableSet<Type> requiredExtensions)
        {
            Plugin = plugin;
            Pass = pass;
            Phase = phase;
            RequiredExtensions = requiredExtensions;
            CompatibleExtensions = compatibleExtensions;
        }

        public override string ToString()
        {
            return Pass.DisplayName;
        }
    }
}