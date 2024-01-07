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

    /// <summary>
    /// This class allows you to set a particular Object Registry instance as the current one to be used for static
    /// methods. This is primarily intended for unit testing.
    /// </summary>
    public sealed class ObjectRegistryScope : IDisposable
    {
        private readonly ObjectRegistry _oldRegistry;

        public ObjectRegistryScope(ObjectRegistry registry)
        {
            _oldRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = registry;
        }

        public void Dispose()
        {
            ObjectRegistry.ActiveRegistry = _oldRegistry;
        }
    }

    internal struct Entry
    {
        public UnityObject Object;
        public ObjectReference Reference;
    }

    /// <summary>
    /// The ObjectRegistry tracks the original position of objects on the avatar; this is used to be able to identify
    /// the source of errors after objects have been moved within the hierarchy.
    /// </summary>
    public sealed class ObjectRegistry
    {
        // Reference hash code => objects
        private readonly Dictionary<int, List<Entry>> _obj2ref =
            new Dictionary<int, List<Entry>>();

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
                _obj2ref[RuntimeHelpers.GetHashCode(obj)] = refs = new List<Entry>();
            }

            foreach (var r in refs)
            {
                if (r.Object == obj) return r.Reference;
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

            refs.Add(new Entry()
            {
                Object = obj,
                Reference = objref
            });

            return objref;
        }

        /// <summary>
        /// Record that a particular object (asset or scene object) was replaced by a clone or transformed version.
        /// This will be used to track the original object in error reports.
        /// </summary>
        /// <param name="oldObject"></param>
        /// <param name="newObject"></param>
        /// <returns>The ObjectReference for the objects in question</returns>
        public static ObjectReference RegisterReplacedObject(UnityObject oldObject, UnityObject newObject)
        {
            return RegisterReplacedObject(GetReference(oldObject), newObject);
        }

        /// <summary>
        /// Record that a particular object (asset or scene object) was replaced by a clone or transformed version.
        /// This will be used to track the original object in error reports.
        /// </summary>
        /// <param name="oldObject"></param>
        /// <param name="newObject"></param>
        /// <returns>The ObjectReference for the objects in question</returns>
        public static ObjectReference RegisterReplacedObject(ObjectReference oldObject, UnityObject newObject)
        {
            if (ActiveRegistry == null) return oldObject;

            if (oldObject == null) throw new NullReferenceException("oldObject must not be null");
            if (newObject == null) throw new NullReferenceException("newObject must not be null");

            if (!ActiveRegistry._obj2ref.TryGetValue(RuntimeHelpers.GetHashCode(newObject), out var refs))
            {
                ActiveRegistry._obj2ref[RuntimeHelpers.GetHashCode(newObject)] = refs = new List<Entry>();
            }

            foreach (var r in refs)
            {
                if (r.Object == newObject)
                {
                    throw new ArgumentException(
                        "RegisterReplacedObject must be called before GetReference is called on the new object");
                }
            }

            refs.Add(new Entry()
            {
                Object = newObject,
                Reference = oldObject
            });

            return oldObject;
        }
    }
}