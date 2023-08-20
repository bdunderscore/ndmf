using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.build_framework;
using nadena.dev.build_framework.model;

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
        public ImmutableList<ConcretePass> Passes { get; private set; }
        
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
            var plugins = new List<InstantiatedPlugin>(
                pluginTemplates.Select(t => new InstantiatedPlugin(t))
            );

            var phases = (IList<BuiltInPhase>) Enum.GetValues(typeof(BuiltInPhase));
            
            var passes = plugins.SelectMany(p => p.Passes)
                .Union(phases.Select(p => new InstantiatedPass((BuiltInPhase)p)));
            var passNames = passes
                .Select(p => new KeyValuePair<string, InstantiatedPass>(p.QualifiedName, p))
                .ToImmutableDictionary();
            var constraints = passes.SelectMany(p => p.Constraints)
                .Where((tuple, i) => passNames.ContainsKey(tuple.Item1) && passNames.ContainsKey(tuple.Item2))
                .Select((tuple, i) => (passNames[tuple.Item1], passNames[tuple.Item2]))
                .ToList();

            Passes = TopoSort.DoSort(passes, constraints)
                .Select(p => new ConcretePass(p.QualifiedName, p.Operation))
                .ToImmutableList();
        }
    }
}