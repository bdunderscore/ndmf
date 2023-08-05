using System;
using System.Collections.Generic;
using nadena.dev.build_framework.util;
using UnityEditor;
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
        
        private Dictionary<Type, object> _contextData = new Dictionary<Type, object>();

        public T Get<T>() where T : new()
        {
            if (_contextData.TryGetValue(typeof(T), out var value))
            {
                return (T) value;
            }

            value = new T();
            _contextData[typeof(T)] = value;
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
        }

        public void Serialize()
        {
            foreach (var asset in _avatarRootObject.ReferencedAssets(traverseSaved: false, includeScene: false))
            {
                AssetDatabase.AddObjectToAsset(asset, AssetContainer);
            }
        }
    }
}