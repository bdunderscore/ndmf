using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.build_framework;
using nadena.dev.build_framework.model;
using UnityEngine;

namespace nadena.dev.build_framework
{
    public class ConcretePass
    {
        public string Description { get; }
        public Action<BuildContext> Process { get; }
        internal InstantiatedPass InstantiatedPass { get; }
        
        internal ConcretePass(InstantiatedPass pass)
        {
            Description = pass.QualifiedName;
            Process = pass.Operation;
            InstantiatedPass = pass;
        }
    }

    public class PluginResolver
    {
        public ImmutableDictionary<BuiltInPhase, ImmutableList<ConcretePass>> Passes { get; private set; }
        
        public PluginResolver() : this(
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(
                    assembly => assembly.GetCustomAttributes(typeof(ExportsPlugin), false))
                .Select(export => ((ExportsPlugin) export).PluginType)
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
                    ToposortPasses(kvp.Value))
            ).ToImmutableDictionary();
        }

        ImmutableList<ConcretePass> ToposortPasses(List<InstantiatedPass> passes)
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
            
            return TopoSort.DoSort(passes, constraints)
                .Select(p => new ConcretePass(p))
                .ToImmutableList();
        } 
    }
}