#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.rq;
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
        private readonly IObjectRegistry _oldRegistry;

        public ObjectRegistryScope(IObjectRegistry registry)
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

    public interface IObjectRegistry
    {
        /// <summary>
        ///     Returns the ObjectReference for the given object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="create">If true, an ObjectReference will be created if one does not already exist</param>
        /// <returns>The ObjectReference, or null if create is false and no reference exists</returns>
        public ObjectReference GetReference(UnityObject obj, bool create = true);

        /// <summary>
        ///     Record that a particular object (asset or scene object) was replaced by a clone or transformed version.
        ///     This will be used to track the original object in error reports.
        /// </summary>
        /// <param name="oldObject"></param>
        /// <param name="newObject"></param>
        /// <returns>The ObjectReference for the objects in question</returns>
        public ObjectReference RegisterReplacedObject(UnityObject oldObject, UnityObject newObject);

        /// <summary>
        ///     Record that a particular object (asset or scene object) was replaced by a clone or transformed version.
        ///     This will be used to track the original object in error reports.
        /// </summary>
        /// <param name="oldObject"></param>
        /// <param name="newObject"></param>
        /// <returns>The ObjectReference for the objects in question</returns>
        public ObjectReference RegisterReplacedObject(ObjectReference oldObject, UnityObject newObject);
    }

    /// <summary>
    /// The ObjectRegistry tracks the original position of objects on the avatar; this is used to be able to identify
    /// the source of errors after objects have been moved within the hierarchy.
    /// </summary>
    public sealed class ObjectRegistry : IObjectRegistry
    {
        // Reference hash code => objects
        private readonly Dictionary<Object, ObjectReference> _obj2ref = new(new ObjectIdentityComparer<UnityObject>());

        internal readonly Transform AvatarRoot;
        public static IObjectRegistry ActiveRegistry { get; internal set; }
        private readonly ObjectRegistry _parent;

        internal static ObjectRegistry Merge(Transform avatarRoot, IEnumerable<ObjectRegistry> inputs)
        {
            var newRegistry = new ObjectRegistry(null);

            foreach (var kvp in inputs.SelectMany(FlattenEntries)) newRegistry._obj2ref[kvp.Key] = kvp.Value;

            return newRegistry;

            IEnumerable<KeyValuePair<Object, ObjectReference>> FlattenEntries(ObjectRegistry registry)
            {
                while (registry != null)
                {
                    foreach (var kvp in registry._obj2ref) yield return kvp;

                    registry = registry._parent;
                }
            }
        }

        public ObjectRegistry(Transform avatarRoot, ObjectRegistry parent = null)
        {
            AvatarRoot = avatarRoot;
            _parent = parent;
        }

        /// <summary>
        ///     Returns the ObjectReference for the given object, using the ambient ObjectRegistry.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static ObjectReference GetReference(UnityObject obj)
        {
            if (obj == null) return null;

            return ActiveRegistry?.GetReference(obj) ?? new ObjectReference(obj, null);
        }

        ObjectReference IObjectRegistry.GetReference(UnityObject obj, bool create)
        {
            var objref = _obj2ref.GetValueOrDefault(obj);

            if (objref != null || !create)
            {
                return objref;
            }

            string path = null;
            if (AvatarRoot == null)
                path = "<unknown>";
            else if (obj is GameObject go)
                path = RuntimeUtil.RelativePath(AvatarRoot?.gameObject, go);
            else if (obj is Component c) path = RuntimeUtil.RelativePath(AvatarRoot?.gameObject, c.gameObject);

            objref = new ObjectReference(obj, path);
            _obj2ref[obj] = objref;

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
            return ActiveRegistry?.RegisterReplacedObject(GetReference(oldObject), newObject);
        }

        ObjectReference IObjectRegistry.RegisterReplacedObject(UnityObject oldObject, UnityObject newObject)
        {
            // We made the mistake of creating a bunch of static wrappers that conflict with the names we want on the
            // interface; work around this by using the interface explicitly here.
            return ((IObjectRegistry)this).RegisterReplacedObject(GetReference(oldObject), newObject);
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
            return ActiveRegistry?.RegisterReplacedObject(oldObject, newObject) ?? oldObject;
        }

        ObjectReference IObjectRegistry.RegisterReplacedObject(ObjectReference oldObject, UnityObject newObject)
        {
            if (oldObject == null) throw new NullReferenceException("oldObject must not be null");
            if (newObject == null) throw new NullReferenceException("newObject must not be null");

            var self = (IObjectRegistry)this;

            if (self.GetReference(newObject, false) != null)
                throw new ArgumentException(
                    "RegisterReplacedObject must be called before GetReference is called on the new object");

            _obj2ref[newObject] = oldObject;

            return oldObject;
        }
    }
}