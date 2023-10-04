#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace nadena.dev.ndmf.model
{
    internal class InnatePhases
    {
        public readonly SolverPass PluginStart, PluginEnd;

        internal InnatePhases(BuildPhase phase, string pluginQualifiedName)
        {
            AnonymousPass startAnon = new AnonymousPass(pluginQualifiedName + "/#innate#start/" + phase,
                "PluginStart: " + pluginQualifiedName,
                ctx => { });
            AnonymousPass endAnon = new AnonymousPass(pluginQualifiedName + "/#innate#end/" + phase,
                "PluginEnd: " + pluginQualifiedName,
                ctx => { });

            startAnon.IsPhantom = true;
            endAnon.IsPhantom = true;

            PluginStart = new SolverPass(InternalPasses.Instance, startAnon, phase,
                ImmutableHashSet<string>.Empty, ImmutableHashSet<Type>.Empty);
            PluginEnd = new SolverPass(InternalPasses.Instance, endAnon, phase,
                ImmutableHashSet<string>.Empty, ImmutableHashSet<Type>.Empty);
        }
    }

    internal class SolverContext
    {
        public List<SolverPass> Passes { get; } = new List<SolverPass>();
        public List<Constraint> Constraints { get; } = new List<Constraint>();

        private Dictionary<(string, BuildPhase), InnatePhases> _innatePhases =
            new Dictionary<(string, BuildPhase), InnatePhases>();

        public InnatePhases GetPluginPhases(BuildPhase phase, string pluginQualifiedName)
        {
            if (!_innatePhases.TryGetValue((pluginQualifiedName, phase), out var phases))
            {
                phases = new InnatePhases(phase, pluginQualifiedName);
                _innatePhases[(pluginQualifiedName, phase)] = phases;
                Passes.Add(phases.PluginStart);
                Passes.Add(phases.PluginEnd);
            }

            return phases;
        }

        public void AddPass(SolverPass pass)
        {
            Passes.Add(pass); // TODO - check duplicates early
        }
    }
}