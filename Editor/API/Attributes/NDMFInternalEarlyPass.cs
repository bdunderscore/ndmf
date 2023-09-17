#region

using System;

#endregion

namespace nadena.dev.ndmf
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal class NDMFInternalEarlyPass : Attribute
    {
    }
}