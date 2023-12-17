#region

using System;
using System.Runtime.CompilerServices;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf
{
    /// <summary>
    /// The ObjectReference class gives you a way to reference an object that may have been moved or destroyed since
    /// its initial creation.
    /// </summary>
    public sealed class ObjectReference
    {
        private readonly Object _obj;
        private readonly string _path;
        private readonly Type _type;

        internal ObjectReference(Object obj, string path)
        {
            _obj = obj;
            _path = path;
            _type = obj.GetType();
        }

        public Object Object => _obj;
        public string Path => _path;
        public Type Type => _type;

        private bool Equals(ObjectReference other)
        {
            return ReferenceEquals(_obj, other._obj);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is ObjectReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(_obj);
        }
    }
}