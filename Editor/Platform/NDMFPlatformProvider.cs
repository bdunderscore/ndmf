using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf.platform
{
    /// <summary>
    /// This attribute marks implementations of NDMF platforms, and registers them for use.
    /// </summary>
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse]
    public sealed class NDMFPlatformProvider : Attribute
    {
        
    }
}