#region

using System;
using System.Threading;
using nadena.dev.ndmf.fluent;

#endregion

namespace nadena.dev.ndmf
{
    internal interface IPass
    {
        string QualifiedName { get; }
        string DisplayName { get; }

        PassKey PassKey { get; }
        bool IsPhantom { get; }

        void Execute(BuildContext context);
    }

    internal class AnonymousPass : IPass
    {
        public string QualifiedName { get; }
        public string DisplayName { get; }

        private readonly InlinePass _executor;
        public PassKey PassKey => new PassKey(QualifiedName);

        internal bool IsPhantom { get; set; }
        bool IPass.IsPhantom => IsPhantom;

        public AnonymousPass(string qualifiedName, string displayName, InlinePass execute)
        {
            QualifiedName = qualifiedName;
            DisplayName = displayName;
            _executor = execute;
        }

        public void Execute(BuildContext context)
        {
            _executor(context);
        }
    }

    public abstract class Pass<T> : IPass where T : Pass<T>, new()
    {
        private static Lazy<T> _instance = new Lazy<T>(() => new T(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        public static T Instance => _instance.Value;

        PassKey IPass.PassKey => new PassKey(QualifiedName);

        public virtual string QualifiedName => typeof(T).FullName;
        public virtual string DisplayName => typeof(T).Name;
        bool IPass.IsPhantom => false;

        protected abstract void Execute(BuildContext context);

        // Prevent Pass handles from being used to execute passes arbitrarily by exposing it via an internal interface
        void IPass.Execute(BuildContext context)
        {
            Execute(context);
        }
    }
}