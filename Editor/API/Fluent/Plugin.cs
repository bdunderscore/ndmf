#region

using System;
using System.Threading;
using nadena.dev.ndmf.fluent;

#endregion

namespace nadena.dev.ndmf
{
    internal interface IPlugin
    {
        string QualifiedName { get; }
        string DisplayName { get; }

        void Configure(PluginInfo info);
    }

    public abstract class Plugin<T> : IPlugin where T : Plugin<T>, new()
    {
        private static object _lock = new object();

        private static Lazy<Plugin<T>> _instance = new Lazy<Plugin<T>>(() => new T(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        public static Plugin<T> Instance => _instance.Value;

        private PluginInfo _info;

        public virtual string QualifiedName => typeof(T).FullName;
        public virtual string DisplayName => QualifiedName;

        void IPlugin.Configure(PluginInfo info)
        {
            _info = info;
            try
            {
                Configure();
            }
            finally
            {
                _info = null;
            }
        }

        protected abstract void Configure();

        protected Sequence InPhase(BuildPhase phase)
        {
            if (_info == null)
            {
                throw new Exception("InPhase can only be called from within Configure()");
            }

            return _info.NewSequence(phase);
        }
    }
}