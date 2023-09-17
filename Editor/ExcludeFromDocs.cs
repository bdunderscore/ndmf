#region

using System;

#endregion

namespace nadena.dev.ndmf
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    internal class ExcludeFromDocs : Attribute
    {
    }
}