#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.runtime;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf
{
    #region

    using UnityObject = Object;

    #endregion

    internal sealed class RegistryScope : IDisposable
    {
        private readonly ObjectRegistry _oldRegistry;

        public RegistryScope(ObjectRegistry registry)
        {
            _oldRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = registry;
        }

        public void Dispose()
        {
            ObjectRegistry.ActiveRegistry = _oldRegistry;
        }
    }

    /// <summary>
    /// The ObjectRegistry tracks the original position of objects on the avatar; this is used to be able to identify
    /// the source of errors after objects have been moved within the hierarchy.
    /// </summary>
    public sealed class ObjectRegistry
    {
        // Reference hash code => objects
        private static readonly Dictionary<int, List<ObjectReference>> _obj2ref =
            new Dictionary<int, List<ObjectReference>>();

        static internal ObjectRegistry ActiveRegistry;
        internal readonly Transform AvatarRoot;

        public ObjectRegistry(Transform avatarRoot)
        {
            AvatarRoot = avatarRoot;
        }

        public static ObjectReference GetReference(UnityObject obj)
        {
            if (obj == null) return null;
            
            return ActiveRegistry?._GetReference(obj) ?? new ObjectReference(obj, null);
        }

        private ObjectReference _GetReference(UnityObject obj)
        {
            if (obj == null) return null;
            if (!_obj2ref.TryGetValue(RuntimeHelpers.GetHashCode(obj), out var refs))
            {
                _obj2ref[RuntimeHelpers.GetHashCode(obj)] = refs = new List<ObjectReference>();
            }

            foreach (var r in refs)
            {
                if (r.Object == obj) return r;
            }

            string path = null;
            if (obj is GameObject go)
            {
                path = RuntimeUtil.RelativePath(ActiveRegistry.AvatarRoot.gameObject, go);
            }
            else if (obj is Component c)
            {
                path = RuntimeUtil.RelativePath(ActiveRegistry.AvatarRoot.gameObject, c.gameObject);
            }

            var objref = new ObjectReference(obj, path);

            refs.Add(objref);

            return objref;
        }
    }
}