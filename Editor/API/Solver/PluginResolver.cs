using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.model;
using UnityEngine;

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

    public class ConcretePass
    {
        public string Description { get; }
        public Action<BuildContext> Process { get; }
        internal InstantiatedPass InstantiatedPass { get; }
        internal ImmutableList<Type> DeactivatePlugins { get; }
        internal ImmutableList<Type> ActivatePlugins { get; }

        internal ConcretePass(InstantiatedPass pass, ImmutableList<Type> deactivatePlugins,
            ImmutableList<Type> activatePlugins)
        {
            Description = pass.DisplayName;
            Process = pass.Operation;
            InstantiatedPass = pass;
            DeactivatePlugins = deactivatePlugins;
            ActivatePlugins = activatePlugins;
        }
    }

    public class PluginResolver
    {
        public ImmutableDictionary<BuiltInPhase, ImmutableList<ConcretePass>> Passes { get; private set; }

        public PluginResolver() : this(
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(
                    assembly => assembly.GetCustomAttributes(typeof(ExportsPlugin), false))
                .Select(export => ((ExportsPlugin) export).PluginType)
                .ToImmutableSortedSet(new TypeComparer())
                // Ensure internal passes run first (absent any ordering constraints)
                .Prepend(typeof(InternalPasses.InternalPasses))
        )
        {
        }

        public PluginResolver(IEnumerable<Type> plugins) : this(
            plugins.Select(plugin =>
                plugin.GetConstructor(new Type[0]).Invoke(new object[0]) as Plugin)
        )
        {
        }

        public PluginResolver(IEnumerable<Plugin> pluginTemplates)
        {
            Dictionary<BuiltInPhase, List<InstantiatedPass>> pluginsByPhase =
                new Dictionary<BuiltInPhase, List<InstantiatedPass>>();

            foreach (var template in pluginTemplates)
            {
                var instantiated = new InstantiatedPlugin(template);
                foreach (var pass in instantiated.Passes)
                {
                    var phase = pass.ExecutionPhase;
                    if (!pluginsByPhase.TryGetValue(phase, out var list))
                    {
                        list = new List<InstantiatedPass>();
                        pluginsByPhase[phase] = list;
                    }

                    list.Add(pass);
                }
            }

            Passes = pluginsByPhase.Select(kvp =>
                new KeyValuePair<BuiltInPhase, ImmutableList<ConcretePass>>(
                    kvp.Key,
                    ToposortPasses(kvp.Key, kvp.Value))
            ).ToImmutableDictionary();
        }

        ImmutableList<ConcretePass> ToposortPasses(BuiltInPhase phase, List<InstantiatedPass> passes)
        {
            var passNames = passes
                .Select(p => new KeyValuePair<string, InstantiatedPass>(p.QualifiedName, p))
                .ToImmutableDictionary();
            var constraints = passes.SelectMany(p => p.Constraints)
                .Where((tuple, i) => passNames.ContainsKey(tuple.Item1) && passNames.ContainsKey(tuple.Item2))
                .Select((tuple, i) => (passNames[tuple.Item1], passNames[tuple.Item2]))
                .ToList();

            foreach (var pass in passes)
            {
                Debug.Log($"Pass found: {pass.QualifiedName}");
            }

            var sorted = TopoSort.DoSort(passes, constraints);
            SortedSet<Type> activeExtensions = new SortedSet<Type>(new TypeComparer());

            var concrete = new List<ConcretePass>();
            foreach (var pass in sorted)
            {
                var toDeactivate = new List<Type>();
                var toActivate = new List<Type>();
                activeExtensions.RemoveWhere(t =>
                {
                    if (!pass.IsContextCompatible(t))
                    {
                        toDeactivate.Add(t);
                        return true;
                    }

                    return false;
                });

                foreach (var t in pass.RequiredContexts.ToImmutableSortedSet(new TypeComparer()))
                {
                    if (!activeExtensions.Contains(t))
                    {
                        toActivate.Add(t);
                        activeExtensions.Add(t);
                    }
                }

                concrete.Add(new ConcretePass(pass, toDeactivate.ToImmutableList(), toActivate.ToImmutableList()));
            }

            if (activeExtensions.Count > 0)
            {
                var deactivator = CleanupPlugin.ExtensionDeactivator(phase);
                concrete.Add(new ConcretePass(deactivator,
                    activeExtensions.ToImmutableList(),
                    ImmutableList<Type>.Empty
                ));
            }

            return concrete.ToImmutableList();
        }
    }
}