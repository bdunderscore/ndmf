using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace nadena.dev.build_framework.model
{
    internal class InstantiatedPlugin
    {
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
            passes.Add(new InstantiatedPass(this, ConstraintType.RunsAfter));

            foreach (var pass in Plugin.Passes)
            {
                var newPass = new InstantiatedPass(this, pass);
                newPass.AddRunsAfter(passes.Last().QualifiedName);
                passes.Add(newPass);
            }

            var finalPass = new InstantiatedPass(this, ConstraintType.RunsBefore);
            finalPass.AddRunsAfter(passes.Last().QualifiedName);
            passes.Add(finalPass);

            Passes = passes.ToImmutableList();
        }

        internal string QualifiedName { get; private set; }
        internal Plugin Plugin { get; private set;  }

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

        internal InstantiatedPass(InstantiatedPlugin parent, ConstraintType constraintType)
        {
            Operation = _ctx => { };
            InternalPass = true;
            _compatibleContexts = null;
            RequiredContexts = ImmutableHashSet<Type>.Empty;
            
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
}