#region

using System;

#endregion

namespace nadena.dev.ndmf
{
    /// <summary>
    /// This attribute declares a plugin to be registered with NDMF.
    ///
    /// <code>
    /// [assembly: ExportsPlugin(typeof(MyPlugin))]
    ///
    /// class MyPlugin : Plugin&lt;MyPlugin> {
    ///   // ...
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ExportsPlugin : Attribute
    {
        public Type PluginType { get; }

        public ExportsPlugin(Type pluginType)
        {
            PluginType = pluginType;
        }
    }
}