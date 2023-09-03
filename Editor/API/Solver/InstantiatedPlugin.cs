using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace nadena.dev.ndmf.model
{
    internal class InstantiatedPlugin
    {
        public string Description => Plugin.Description;

        internal InstantiatedPlugin(Type target)
        {
            if (!typeof(Plugin).IsAssignableFrom(target))
            {
                throw new Exception("Unable to load plugin " + target + ": Plugin type must be assignable from Plugin");
            }

            Plugin = (Plugin) target.GetConstructor(new Type[0]).Invoke(new object[0]);

            Init(Plugin);
        }

        internal InstantiatedPlugin(Plugin plugin)
        {
            Init(plugin);
        }

        void Init(Plugin plugin)
        {
            Plugin = plugin;
            QualifiedName = Plugin.QualifiedName;

            var passes = new List<InstantiatedPass>();

            foreach (var builtInPhaseObj in Enum.GetValues(typeof(BuiltInPhase)))
            {
                var phase = (BuiltInPhase) builtInPhaseObj;
                var phasePasses = plugin.Passes.Where(p => p.ExecutionPhase == phase).ToList();

                if (phasePasses.Count == 0) continue;

                passes.Add(new InstantiatedPass(this, ConstraintType.RunsAfter, phase));

                foreach (var pass in phasePasses)
                {
                    var newPass = new InstantiatedPass(this, pass);
                    newPass.AddRunsAfter(passes.Last().QualifiedName);
                    passes.Add(newPass);
                }

                var finalPass = new InstantiatedPass(this, ConstraintType.RunsBefore, phase);
                finalPass.AddRunsAfter(passes.Last().QualifiedName);
                passes.Add(finalPass);
            }

            Passes = passes.ToImmutableList();
        }

        internal string QualifiedName { get; private set; }
        internal Plugin Plugin { get; private set; }

        public IEnumerable<(string, string)> PassConstraints =>
            Passes.SelectMany(p => p.Constraints);

        internal ImmutableList<InstantiatedPass> Passes;
    }

    enum ConstraintType
    {
        RunsBefore,
        RunsAfter,
    }

    internal class InstantiatedPass
    {
        public readonly string BEFORE_PLUGIN_HOOK = "/_internal/BeforePlugin";
        public readonly string AFTER_PLUGIN_HOOK = "/_internal/AfterPlugin";

        public InstantiatedPlugin Plugin { get; }
        public BuiltInPhase ExecutionPhase { get; }

        public bool InternalPass { get; }
        public string QualifiedName { get; }
        public string DisplayName { get; }

        public Action<BuildContext> Operation { get; }

        public ImmutableList<(string, string)> Constraints { get; private set; }

        private ImmutableHashSet<string> _compatibleContexts { get; }
        public IImmutableSet<Type> RequiredContexts { get; }

        public bool IsContextCompatible(Type contextType)
            => _compatibleContexts == null
               || _compatibleContexts.Contains(contextType.FullName)
               || RequiredContexts.Contains(contextType);

        internal void AddRunsAfter(string other)
        {
            Constraints = Constraints.Add((other, QualifiedName));
        }

        internal InstantiatedPass(InstantiatedPlugin parent, PluginPass pass)
        {
            Plugin = parent;

            var passType = pass.GetType();
            Operation = pass.Process;
            InternalPass = false;
            QualifiedName = parent.QualifiedName + "/" + passType.Name;
            DisplayName = pass.DisplayName;
            ExecutionPhase = pass.ExecutionPhase;

            _compatibleContexts = pass.CompatibleContexts.Select(ctx =>
            {
                switch (ctx)
                {
                    case string s: return s;
                    case Type ty: return ty.FullName;
                    default: throw new Exception("Invalid context type " + ctx + " (must be string or Type)");
                }
            }).ToImmutableHashSet();

            RequiredContexts = pass.RequiredContexts;
            if (RequiredContexts == null)
            {
                throw new Exception("RequiredContexts must not be null (for pass " + pass + ")");
            }

            var constraints = new List<(string, string)>();
            foreach (var before in pass.RunsBefore)
            {
                constraints.Add((QualifiedName, before));
            }

            foreach (var after in pass.RunsAfter)
            {
                constraints.Add((after, QualifiedName));
            }

            Constraints = constraints.ToImmutableList();
        }

        internal InstantiatedPass(InstantiatedPlugin parent, ConstraintType constraintType, BuiltInPhase builtInPhase)
        {
            Plugin = parent;

            Operation = _ctx => { };
            InternalPass = true;
            _compatibleContexts = null;
            RequiredContexts = ImmutableHashSet<Type>.Empty;
            ExecutionPhase = builtInPhase;

            IEnumerable<(string, string)> constraints;
            switch (constraintType)
            {
                case ConstraintType.RunsAfter:
                {
                    QualifiedName = parent.QualifiedName + BEFORE_PLUGIN_HOOK;
                    constraints = parent.Plugin.RunsAfter.Select(x => (x + AFTER_PLUGIN_HOOK, QualifiedName));

                    break;
                }
                case ConstraintType.RunsBefore:
                {
                    QualifiedName = parent.QualifiedName + AFTER_PLUGIN_HOOK;
                    constraints = parent.Plugin.RunsBefore.Select(x => (QualifiedName, x + BEFORE_PLUGIN_HOOK));

                    break;
                }
                default:
                {
                    throw new Exception("Invalid constraint type " + constraintType);
                }
            }

            DisplayName = QualifiedName;
            Constraints = constraints.ToImmutableList();
        }

        public override string ToString()
        {
            return $"{nameof(QualifiedName)}: {QualifiedName}, {nameof(DisplayName)}: {DisplayName}";
        }
    }

    internal class CleanupPlugin : Plugin
    {
        private static InstantiatedPlugin InstantiatedPlugin = new InstantiatedPlugin(new CleanupPlugin());

        public override string QualifiedName => "nadena.dev.av3-build-framework.internal.cleanup";
        public override string Description => "Cleanup";
        public override ImmutableList<PluginPass> Passes => ImmutableList<PluginPass>.Empty;

        public static InstantiatedPass ExtensionDeactivator(BuiltInPhase phase)
        {
            return new InstantiatedPass(InstantiatedPlugin, new Pass(phase));
        }

        internal class Pass : PluginPass
        {
            public override string DisplayName => "Deactivate extensions";

            public override BuiltInPhase ExecutionPhase { get; }

            public Pass(BuiltInPhase phase)
            {
                ExecutionPhase = phase;
            }

            public override void Process(BuildContext context)
            {
                // no-op - this just serves to deactivate extension contexts
            }
        }
    }
}