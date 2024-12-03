using System;

namespace nadena.dev.ndmf.runtime
{
    /// <summary>
    /// This class marks types which are currently considered experimental. Experimental types may change without notice,
    /// and may not function properly unless the NDMF_EXPERIMENTAL define is set. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class NDMFExperimental : Attribute
    {
        
    }
}