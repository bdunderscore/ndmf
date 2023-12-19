using System;

namespace nadena.dev.ndmf
{
    public struct ErrorContext
    {
        public IError TheError;
        public PluginBase Plugin;
        public string PassName;
        public Type ExtensionContext;
    }
}