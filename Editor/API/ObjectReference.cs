#region

using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
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
        private readonly string _description;

        internal ObjectReference(Object obj, string path)
        {
            _obj = obj;
            _path = path;
            _type = obj?.GetType();

            if (_path != null)
            {
                _description = _path;
            }
            else
            {
                _description = obj.name;
            }
        }

        public Object Object => _obj;
        public string Path => _path;
        public Type Type => _type;

        public override string ToString()
        {
            return _description;
        }

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

        public bool TryResolve(ErrorReport report, out Object obj)
        {
            obj = null;

            if (_obj != null && EditorUtility.IsPersistent(_obj))
            {
                // We're referencing an asset.
                obj = _obj;
                return true;
            }

            if (!report.TryResolveAvatar(out var av))
            {
                return false;
            }

            if (av == null || _path == null) return false;

            GameObject go = av.transform.Find(_path)?.gameObject;

            if (go == null) return false;
            if (Type == typeof(GameObject))
            {
                obj = go;
            }
            else
            {
                obj = go.GetComponent(Type);
            }

            return obj != null;
        }
    }
}