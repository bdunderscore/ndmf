using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace nadena.dev.ndmf
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public class ExportsPlugin : System.Attribute
    {
        public Type PluginType;

        public ExportsPlugin(Type pluginType)
        {
            PluginType = pluginType;
        }
    }

    public abstract class Plugin
    {
        public abstract string QualifiedName { get; }
        public virtual bool EnableByDefault => true;

        public abstract ImmutableList<PluginPass> Passes { get; }

        public virtual ImmutableList<string> RunsBefore => ImmutableList<string>.Empty;
        public virtual ImmutableList<string> RunsAfter => ImmutableList<string>.Empty;
        public virtual ImmutableList<string> RequiredPlugins => ImmutableList<string>.Empty;
        public virtual ImmutableList<string> RequiredPasses => ImmutableList<string>.Empty;
        public virtual string Description => QualifiedName;
    }

    public abstract class PluginPass
    {
        public virtual BuiltInPhase ExecutionPhase => BuiltInPhase.Transforming;
        public virtual string DisplayName => GetType().Name;

        public virtual ImmutableList<string> RunsBefore => ImmutableList<string>.Empty;
        public virtual ImmutableList<string> RunsAfter => ImmutableList<string>.Empty;

        // Type or string
        public virtual IImmutableSet<object> CompatibleContexts => ImmutableHashSet<object>.Empty;
        public virtual IImmutableSet<Type> RequiredContexts => ImmutableHashSet<Type>.Empty;

        public abstract void Process(BuildContext context);
    }
}