using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf.platform
{
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse]
    internal sealed class NDMFPlatformProvider : Attribute
    {
        
    }
}