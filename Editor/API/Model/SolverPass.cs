#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview;

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
        internal List<IRenderFilter> RenderFilters { get; } = new();

        internal bool IsExtensionCompatible(Type ty, ISet<Type> activeExtensions)
        {
            if (IsPhantom || RequiredExtensions.Contains(ty) || CompatibleExtensions.Contains(ty.FullName))
            {
                return true;
            }

            // See if any of the active extensions depends on the given type, and if so, if we are compatible with it.
            foreach (var active in activeExtensions)
            {
                if (!CompatibleExtensions.Contains(active.FullName) && !RequiredExtensions.Contains(active))
                {
                    continue;
                }

                if (active.ContextDependencies(true).Contains(ty)) return true;
            }

            return false;
        }

        internal SolverPass(IPluginInternal plugin, IPass pass, BuildPhase phase, IImmutableSet<string> compatibleExtensions,
            IImmutableSet<Type> requiredExtensions)
        {
            Plugin = plugin;
            Pass = pass;
            Phase = phase;
            CompatibleExtensions = compatibleExtensions;
            RequiredExtensions = requiredExtensions.Union(pass.GetType().ContextDependencies());
        }

        public override string ToString()
        {
            return Pass.DisplayName;
        }
    }
}