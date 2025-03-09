#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.ndmf.model;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.UI;

#endregion

namespace nadena.dev.ndmf
{
    class TypeComparer : IComparer<Type>
    {
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
        public static event Action<string, bool> OnPluginDisableChanged;

        private const string SessionStateKey = "nadena.dev.ndmf.plugin-disabled.";

        public static bool IsPluginDisabled(string pluginId) => UnityEditor.SessionState.GetBool(SessionStateKey + pluginId, false);
        public static void SetPluginDisabled(string pluginId, bool state)
        {
            UnityEditor.SessionState.SetBool(SessionStateKey + pluginId, state);
            OnPluginDisableChanged?.Invoke(pluginId, state);
        }
    }

    internal class PluginResolver
    {
        internal ImmutableList<(BuildPhase, IList<ConcretePass>)> Passes { get; }

        private readonly List<SolverPass> _allPasses = new();

        public static IEnumerable<Type> FindPluginTypes() => AppDomain.CurrentDomain.GetAssemblies().SelectMany(
                assembly => assembly.GetCustomAttributes(typeof(ExportsPlugin), false))
            .Select(export => ((ExportsPlugin)export).PluginType)
            .ToImmutableSortedSet(new TypeComparer())
            .Prepend(typeof(InternalPasses));

        [CanBeNull] private static IPluginInternal InstantiatePlugin(Type pluginType) =>
            pluginType.GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>()) as IPluginInternal;

        [ItemCanBeNull]
        public static IEnumerable<IPluginInternal> FindAllPlugins() => FindPluginTypes().Select(InstantiatePlugin);

        public PluginResolver(bool includeDisabled = false) : this(FindPluginTypes(), includeDisabled)
        {
        }

        public PluginResolver(IEnumerable<Type> plugins, bool includeDisabled = false) 
            : this(plugins.Select(InstantiatePlugin), includeDisabled)
        {
        }

        public PluginResolver(IEnumerable<IPluginInternal> pluginTemplates, bool includeDisabled = false)
        {
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
                foreach (var t in activeExtensions.OrderByDescending(
                             t => (t.ContextDependencies(true).Count(), t.FullName)
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

                foreach (var dep in new SortedSet<Type>(ty.ContextDependencies(), new TypeComparer()))
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