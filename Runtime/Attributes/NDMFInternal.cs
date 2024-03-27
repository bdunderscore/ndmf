using System;

namespace nadena.dev.ndmf.runtime
{
    /// <summary>
    /// This annotation marks components and other types that are internal to NDMF that might be visible to other
    /// extensions. For API compatibility reasons, these types may not be public, but they'll be marked to allow tools
    /// such as AAO to ignore them.
    ///
    /// We do not recommend using this attribute on your own classes. NDMF does not use it directly, but other plugins
    /// might in unpredictable ways.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class NDMFInternal : Attribute
    {
        
    }
}