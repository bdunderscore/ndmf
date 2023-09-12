using System;

namespace nadena.dev.ndmf
{
    [System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    internal class ExcludeFromDocs : Attribute
    {
        
    }
}