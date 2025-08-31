#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using nadena.dev.ndmf.model;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.UI;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    class TypeComparer : IComparer<Type>
    {
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
        public int Compare(Type x, Type y)
        {
            if (x == y) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            return StringComparer.Ordinal.Compare(x.FullName, y.FullName);
        }
    }

    internal class ConcretePass
    {
        internal IPluginInternal Plugin;
        internal string Description { get; }
        internal IPass InstantiatedPass { get; }
        internal ImmutableList<Type> DeactivatePlugins { get; }
        internal ImmutableList<Type> ActivatePlugins { get; }
        internal ImmutableList<IRenderFilter> RenderFilters { get; }
        internal bool HasPreviews => RenderFilters.Any();
        public bool Skipped { get; set; }

        public void Execute(BuildContext context)
        {
            InstantiatedPass.Execute(context);
        }

        internal ConcretePass(IPluginInternal plugin, IPass pass, ImmutableList<Type> deactivatePlugins,
            ImmutableList<Type> activatePlugins, ImmutableList<IRenderFilter> renderFilters)
        {
            Plugin = plugin;
            Description = pass.DisplayName;
            InstantiatedPass = pass;
            DeactivatePlugins = deactivatePlugins;
            ActivatePlugins = activatePlugins;
            RenderFilters = renderFilters;
        }
    }

    /// <summary>
    /// This class stores user preferences to control whether plugins are disabled.
    /// </summary>
    internal static class PluginDisablePrefs
    {
        // parameter: plugin id, new state
        public static event Action<string, bool>? OnPluginDisableChanged;

        private const string SessionStateKey = "nadena.dev.ndmf.plugin-disabled.";

        public static bool IsPluginDisabled(string pluginId)
        {
            return SessionState.GetBool(SessionStateKey + pluginId, false);
        }

        public static void SetPluginDisabled(string pluginId, bool state)
        {
            SessionState.SetBool(SessionStateKey + pluginId, state);
            OnPluginDisableChanged?.Invoke(pluginId, state);
        }
    }

    internal class PluginResolver
    {
        internal ImmutableList<(BuildPhase, IList<ConcretePass>)> Passes { get; }

        private readonly List<SolverPass> _allPasses = new();
        private INDMFPlatformProvider _platform;

        public static IEnumerable<Type> FindPluginTypes() => AppDomain.CurrentDomain.GetAssemblies().SelectMany(
                assembly => assembly.GetCustomAttributes(typeof(ExportsPlugin), false))
            .Select(export => ((ExportsPlugin)export).PluginType)
            .ToImmutableSortedSet(new TypeComparer())
            .Prepend(typeof(InternalPasses));

        private static IPluginInternal? InstantiatePlugin(Type pluginType)
        {
            var plugin = pluginType.GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>()) as IPluginInternal;
            if (plugin == null)
            {
                Debug.LogWarning($"Failed to instantiate plugin of type {pluginType.FullName}. Plugin may not have a parameterless constructor or may not implement IPluginInternal.");
            }
            return plugin;
        }

        public static IEnumerable<IPluginInternal> FindAllPlugins() => FindPluginTypes().Select(InstantiatePlugin).Where(p => p != null)!;

        public PluginResolver(INDMFPlatformProvider? platform = null, bool includeDisabled = false) : this(FindPluginTypes(), platform, includeDisabled)
        {
        }

        internal PluginResolver(IEnumerable<Type> plugins, INDMFPlatformProvider? platform, bool includeDisabled = false) 
            : this(plugins.Select(pluginType => {
                var plugin = InstantiatePlugin(pluginType);
                if (plugin == null)
                {
                    throw new InvalidOperationException($"Failed to instantiate plugin of type {pluginType.FullName}. Plugin must have a parameterless constructor and implement IPluginInternal.");
                }
                return plugin;
            }), platform, includeDisabled)
        {
        }

        public PluginResolver(IEnumerable<IPluginInternal> pluginTemplates, INDMFPlatformProvider? platform, bool includeDisabled = false)
        {
            platform ??= AmbientPlatform.CurrentPlatform;
            _platform = platform;
            
            var solverContext = new SolverContext();

            foreach (var plugin in pluginTemplates)
            {
                var pluginInfo = new PluginInfo(solverContext, plugin);
                plugin.Configure(pluginInfo);
            }

            Dictionary<string, SolverPass> passByName = new Dictionary<string, SolverPass>();
            Dictionary<BuildPhase, List<SolverPass>> passesByPhase = new Dictionary<BuildPhase, List<SolverPass>>();
            Dictionary<BuildPhase, List<(SolverPass, SolverPass, ConstraintType)>>
                constraintsByPhase = new Dictionary<BuildPhase, List<(SolverPass, SolverPass, ConstraintType)>>();
            
            foreach (var pass in solverContext.Passes)
            {
                if (!passesByPhase.TryGetValue(pass.Phase, out var list))
                {
                    list = new List<SolverPass>();
                    passesByPhase[pass.Phase] = list;
                }

                list.Add(pass);

                if (passByName.ContainsKey(pass.PassKey.QualifiedName))
                {
                    throw new Exception("Duplicate pass with qualified name " + pass.PassKey.QualifiedName);
                }

                passByName[pass.PassKey.QualifiedName] = pass;
            }

            foreach (var constraint in solverContext.Constraints)
            {
                if (!passByName.TryGetValue(constraint.First.QualifiedName, out var first))
                {
                    continue; // optional dependency
                }

                if (!passByName.TryGetValue(constraint.Second.QualifiedName, out var second))
                {
                    continue; // optional dependency
                }

                if (first.Phase != second.Phase)
                {
                    throw new Exception("Cannot add constraint between passes in different phases: " + constraint);
                }

                if (!constraintsByPhase.TryGetValue(first.Phase, out var list))
                {
                    list = new List<(SolverPass, SolverPass, ConstraintType)>();
                    constraintsByPhase[first.Phase] = list;
                }

                list.Add((first, second, constraint.Type));
            }

            ImmutableList<(BuildPhase, IList<ConcretePass>)> result =
                ImmutableList<(BuildPhase, IList<ConcretePass>)>.Empty;

            foreach (var phase in BuildPhase.BuiltInPhases)
            {
                var passes = passesByPhase.TryGetValue(phase, out var list) ? list : null;
                if (passes == null)
                {
                    result = result.Add((phase, ImmutableList<ConcretePass>.Empty));
                    continue;
                }

                IEnumerable<(SolverPass, SolverPass, ConstraintType)> constraints =
                    constraintsByPhase.TryGetValue(phase, out var constraintList)
                        ? constraintList
                        : new List<(SolverPass, SolverPass, ConstraintType)>();
#if NDMF_INTERNAL_DEBUG
                var dumpString = "";
                foreach (var constraint in constraints)
                {
                    dumpString += $"\"{constraint.Item1.PassKey.QualifiedName}\" -> \"{constraint.Item2.PassKey.QualifiedName}\" [label=\"{constraint.Item3}\"];\n";
                }
                Debug.Log(dumpString);
#endif
                
                var sorted = TopoSort.DoSort(passes, constraints);
                if (!includeDisabled) sorted.RemoveAll(pass => PluginDisablePrefs.IsPluginDisabled(pass.Plugin.QualifiedName));
                _allPasses.AddRange(sorted);

                for (int i = 0; i < sorted.Count; i++)
                {
                    if (!sorted[i].IsPlatformCompatible(platform))
                    {
                        // Replace with stub
                        var originalPass = sorted[i].Pass;
                        var pass = new AnonymousPass(originalPass.QualifiedName,
                            "Incompatible: " + originalPass.DisplayName,
                            _ => { });
                        sorted[i] = new SolverPass(sorted[i].Plugin, pass, sorted[i].Phase,
                            sorted[i].CompatibleExtensions, ImmutableHashSet<Type>.Empty);
                        sorted[i].Skipped = true;
                    }
                }
                
                var concrete = ToConcretePasses(phase, sorted);

                result = result.Add((phase, concrete));
            }

            Passes = result;
        }

        ImmutableList<ConcretePass> ToConcretePasses(BuildPhase phase, IEnumerable<SolverPass> sorted)
        {
            HashSet<Type> activeExtensions = new HashSet<Type>();

            var concrete = new List<ConcretePass>();
            foreach (var pass in sorted)
            {
                if (pass.IsPhantom) continue;

                var toDeactivate = new List<Type>();
                var toActivate = new List<Type>();

                // To ensure that we deactivate extensions in the correct order, we sort them by the number of dependencies
                // as a very crude toposort, with name as a tiebreaker (mostly for our tests)
                foreach (var t in activeExtensions.OrderByDescending(t => (t.RequiredContexts(true).Count(), t.FullName)
                         ).ToList())
                {
                    if (!pass.IsExtensionCompatible(t, activeExtensions))
                    {
                        toDeactivate.Add(t);
                        activeExtensions.Remove(t);
                    }
                }

                foreach (var t in ResolveExtensionDependencies(pass.RequiredExtensions))
                {
                    if (!activeExtensions.Contains(t))
                    {
                        toActivate.Add(t);
                        activeExtensions.Add(t);
                    }
                }

                var concretePass = new ConcretePass(pass.Plugin, pass.Pass, toDeactivate.ToImmutableList(),
                    toActivate.ToImmutableList(), pass.RenderFilters.ToImmutableList());
                concretePass.Skipped = pass.Skipped;

                concrete.Add(concretePass);
            }

            if (activeExtensions.Count > 0)
            {
                var cleanup = new AnonymousPass("nadena.dev.ndmf.internal.CleanupExtensions." + phase,
                    "Close extensions",
                    ctx => { });

                concrete.Add(new ConcretePass(InternalPasses.Instance, cleanup,
                    activeExtensions.ToImmutableList(),
                    ImmutableList<Type>.Empty,
                    ImmutableList<IRenderFilter>.Empty
                ));
            }

            return concrete.ToImmutableList();
        }

        private IEnumerable<Type> ResolveExtensionDependencies(IImmutableSet<Type> passRequiredExtensions)
        {
            var resultSet = new HashSet<Type>();
            var results = new List<Type>();
            var stack = new Stack<Type>();

            foreach (var type in new SortedSet<Type>(passRequiredExtensions, new TypeComparer()))
            {
                VisitType(type);
            }

            return results;

            void VisitType(Type ty)
            {
                if (stack.Contains(ty))
                {
                    throw new Exception("Circular dependency detected: " + string.Join(" -> ", stack));
                }

                if (resultSet.Contains(ty)) return;

                stack.Push(ty);

                foreach (var dep in new SortedSet<Type>(ty.RequiredContexts(), new TypeComparer()))
                {
                    VisitType(dep);
                }

                stack.Pop();

                resultSet.Add(ty);
                results.Add(ty);
            }
        }

        internal PreviewSession PreviewSession
        {
            get
            {
                var session = new PreviewSession();

                foreach (var pass in _allPasses)
                {
                    if (!PreviewPrefs.instance.IsPreviewPluginEnabled(pass.Plugin.QualifiedName)) continue;
                    if (PluginDisablePrefs.IsPluginDisabled(pass.Plugin.QualifiedName)) continue;

                    foreach (var filter in pass.RenderFilters)
                        session.AddMutator(new SequencePoint(), filter);
                }

                return session;
            }
        }
    }
}