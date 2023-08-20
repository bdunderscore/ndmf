using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.build_framework.animation;
using nadena.dev.build_framework.util;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.build_framework
{
    public class BuildContext
    {
        private readonly VRCAvatarDescriptor _avatarDescriptor;
        private readonly GameObject _avatarRootObject;
        private readonly Transform _avatarRootTransform;
        
        public VRCAvatarDescriptor AvatarDescriptor => _avatarDescriptor;
        public GameObject AvatarRootObject => _avatarRootObject;
        public Transform AvatarRootTransform => _avatarRootTransform;
        public UnityEngine.Object AssetContainer { get; private set; }
        
        private Dictionary<Type, object> _state = new Dictionary<Type, object>();
        private Dictionary<Type, ExtensionContext> _extensions = new Dictionary<Type, ExtensionContext>();
        private Dictionary<Type, ExtensionContext> _activeExtensions = new Dictionary<Type, ExtensionContext>();
        
        public T GetState<T>() where T : new()
        {
            if (_state.TryGetValue(typeof(T), out var value))
            {
                return (T) value;
            }

            value = new T();
            _state[typeof(T)] = value;
            return (T) value;
        }

        public T Extension<T>() where T : ExtensionContext
        {
            if (!_activeExtensions.TryGetValue(typeof(T), out var value))
            {
                throw new Exception($"Extension {typeof(T)} not active");
            }
            
            return (T) value;
        }

        public BuildContext(GameObject obj, string assetRootPath) 
            : this(obj.GetComponent<VRCAvatarDescriptor>(), assetRootPath)
        {
        }
        
        public BuildContext(VRCAvatarDescriptor avatarDescriptor, string assetRootPath)
        {
            _avatarDescriptor = avatarDescriptor;
            _avatarRootObject = avatarDescriptor.gameObject;
            _avatarRootTransform = avatarDescriptor.transform;
            
            // Ensure the target directory exists
            System.IO.Directory.CreateDirectory(assetRootPath);

            var avatarName = _avatarRootObject.name;
            var avatarPath = System.IO.Path.Combine(assetRootPath, avatarName) + ".asset";

            AssetDatabase.GenerateUniqueAssetPath(avatarPath);
            AssetContainer = ScriptableObject.CreateInstance<GeneratedAssets>();
            AssetDatabase.CreateAsset(AssetContainer, avatarPath);
            Debug.Log($"Setting path for asset container; desired={avatarPath} actual={AssetDatabase.GetAssetPath(AssetContainer)}");
            
            AnimationUtil.CloneAllControllers(this);
        }

        public bool IsTemporaryAsset(UnityEngine.Object obj)
        {
            return !EditorUtility.IsPersistent(obj) 
                   || AssetDatabase.GetAssetPath(obj) == AssetDatabase.GetAssetPath(AssetContainer);
        }

        public void Serialize()
        {
            Debug.Log($"AssetContainer path: {AssetDatabase.GetAssetPath(AssetContainer)}");
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(AssetContainer)))
            {
                throw new Exception("Asset container was lost");
            }
            
            foreach (var asset in _avatarRootObject.ReferencedAssets(traverseSaved: false, includeScene: false))
            {
                //Debug.Log($"Adding asset of type {asset.GetType()}");
                AssetDatabase.AddObjectToAsset(asset, AssetContainer);
            }
        }

        internal void RunPass(ConcretePass pass)
        {
            foreach (var kvp in _activeExtensions.ToList())
            {
                if (!pass.InstantiatedPass.IsContextCompatible(kvp.Key))
                {
                    kvp.Value.OnDeactivate(this);
                    _activeExtensions.Remove(kvp.Key);
                }
            }

            foreach (var ty in pass.InstantiatedPass.RequiredContexts)
            {
                if (!_extensions.TryGetValue(ty, out var ctx))
                {
                    ctx = (ExtensionContext) ty.GetConstructor(Type.EmptyTypes).Invoke(Array.Empty<object>());
                }

                ctx.OnActivate(this);
                _activeExtensions.Add(ty, ctx);
            }
            
            pass.Process(this);
        }

        internal void Finish()
        {
            foreach (var kvp in _activeExtensions.ToList())
            {
                kvp.Value.OnDeactivate(this);
                if (kvp.Value is IDisposable d)
                {
                    d.Dispose();
                }
                _activeExtensions.Remove(kvp.Key);
            }
            
            Serialize();
        }
    }
}