using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf.platform
{
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse]
    public sealed class NDMFPlatformProvider : Attribute
    {
        
    }
}