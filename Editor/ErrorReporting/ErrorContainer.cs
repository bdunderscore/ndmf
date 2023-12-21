using System;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Holds a single error and information about where the error originated from.
    /// </summary>
    public struct ErrorContext
    {
        public IError TheError;
        public PluginBase Plugin;
        public string PassName;
        public Type ExtensionContext;
    }
}