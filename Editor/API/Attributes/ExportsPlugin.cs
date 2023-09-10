using System;

namespace nadena.dev.ndmf
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ExportsPlugin : Attribute
    {
        public Type PluginType { get; }
        
        public ExportsPlugin(Type pluginType)
        {
            PluginType = pluginType;
        }
    }
}