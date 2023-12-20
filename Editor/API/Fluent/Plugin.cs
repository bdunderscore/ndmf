#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using nadena.dev.ndmf.fluent;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    internal interface IPlugin
    {
        string QualifiedName { get; }
        string DisplayName { get; }

        void Configure(PluginInfo info);

        void OnUnhandledException(Exception e);
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

        /// <summary>
        /// The set of platforms for which this plugin will be applied. By default, the plugin is always executed. 
        /// </summary>
        public virtual ISet<AvatarPlatform> SupportedPlatforms => ImmutableHashSet<AvatarPlatform>.Empty
            .Add(AvatarPlatform.Generic);

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

        /// <summary>
        /// Invoked when a pass in this plugin throws an exception. This exception can be passed to a plugin's own error
        /// handling UI.
        ///
        /// This API will likely be deprecated once a native error-reporting system is available.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnUnhandledException(Exception e)
        {
            Debug.LogException(e);
        }

        void IPlugin.OnUnhandledException(Exception e)
        {
            OnUnhandledException(e);
        }
    }
}