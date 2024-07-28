#region

using System.Collections.Generic;
using System.Runtime.CompilerServices;

#endregion

namespace nadena.dev.ndmf
{
    internal class ObjectIdentityComparer<T> : IEqualityComparer<T>
    {
        public static ObjectIdentityComparer<T> Instance { get; } = new ObjectIdentityComparer<T>();


        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}