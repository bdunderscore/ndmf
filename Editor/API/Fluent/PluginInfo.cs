#region

using System.Collections.Generic;
using nadena.dev.ndmf.fluent;
using nadena.dev.ndmf.model;

#endregion

namespace nadena.dev.ndmf
{
    internal class PluginInfo
    {
        private readonly SolverContext _solverContext;
        private readonly IPluginInternal _plugin;
        private int sequenceIndex = 0;
        private HashSet<BuildPhase> _createdInnatePhases = new HashSet<BuildPhase>();

        public PluginInfo(SolverContext solverContext, IPluginInternal plugin)
        {
            _solverContext = solverContext;
            _plugin = plugin;
        }

        internal Sequence NewSequence(BuildPhase phase)
        {
            string sequencePrefix = _plugin.QualifiedName + "/sequence#" + sequenceIndex++;
            return new Sequence(phase, _solverContext, _plugin, sequencePrefix);
        }
    }
}