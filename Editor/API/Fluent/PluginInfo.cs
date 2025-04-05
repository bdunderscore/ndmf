#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        private ImmutableHashSet<string>? _defaultPlatforms;

        public PluginInfo(SolverContext solverContext, IPluginInternal plugin)
        {
            _solverContext = solverContext;
            _plugin = plugin;

            _defaultPlatforms = ImmutableHashSet<string>.Empty.Add(WellKnownPlatforms.VRChatAvatar30);
            if (plugin.GetType().GetCustomAttributes(typeof(RunsOnAllPlatforms), false).Length > 0)
            {
                _defaultPlatforms = null;
            }

            var supportedPlatforms = plugin.GetType().GetCustomAttributes(typeof(RunsOnPlatforms), false)
                .OfType<RunsOnPlatforms>()
                .SelectMany(p => p.Platforms)
                .ToImmutableHashSet();
            if (_defaultPlatforms == null && supportedPlatforms.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Plugin {plugin.GetType().Name} has both [RunsOnAllPlatforms] and [RunsOnPlatform] attributes. Please use one or the other.");
            } else if (supportedPlatforms.Count > 0)
            {
                _defaultPlatforms = supportedPlatforms;
            }
        }

        internal Sequence NewSequence(BuildPhase phase)
        {
            string sequencePrefix = _plugin.QualifiedName + "/sequence#" + sequenceIndex++;
            return new Sequence(phase, _solverContext, _plugin, sequencePrefix, _defaultPlatforms);
        }
    }
}