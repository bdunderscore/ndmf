using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using nadena.dev.build_framework.animation;
using nadena.dev.build_framework.reporting;
using nadena.dev.build_framework.runtime;
using nadena.dev.build_framework.util;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Debug = UnityEngine.Debug;
using UnityObject = UnityEngine.Object;

namespace nadena.dev.build_framework
{
    public class BuildContext
    {
        private readonly VRCAvatarDescriptor _avatarDescriptor;
        private readonly GameObject _avatarRootObject;
        private readonly Transform _avatarRootTransform;

        private Stopwatch sw = new Stopwatch();
        
        public VRCAvatarDescriptor AvatarDescriptor => _avatarDescriptor;
        public GameObject AvatarRootObject => _avatarRootObject;
        public Transform AvatarRootTransform => _avatarRootTransform;
        public UnityEngine.Object AssetContainer { get; private set; }
        
        private Dictionary<Type, object> _state = new Dictionary<Type, object>();
        private Dictionary<Type, IExtensionContext> _extensions = new Dictionary<Type, IExtensionContext>();
        private Dictionary<Type, IExtensionContext> _activeExtensions = new Dictionary<Type, IExtensionContext>();
        
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

        public T Extension<T>() where T : IExtensionContext
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
            BuildEvent.Dispatch(new BuildEvent.BuildStarted(avatarDescriptor.gameObject));
            
            sw.Start();
            
            _avatarDescriptor = avatarDescriptor;
            _avatarRootObject = avatarDescriptor.gameObject;
            _avatarRootTransform = avatarDescriptor.transform;
            
            var avatarName = _avatarRootObject.name;
            
            AssetContainer = ScriptableObject.CreateInstance<GeneratedAssets>();
            if (assetRootPath != null)
            {
                // Ensure the target directory exists
                System.IO.Directory.CreateDirectory(assetRootPath);
                var avatarPath = System.IO.Path.Combine(assetRootPath, avatarName) + ".asset";
                AssetDatabase.GenerateUniqueAssetPath(avatarPath);
                AssetDatabase.CreateAsset(AssetContainer, avatarPath);
            }
            
            AnimationUtil.CloneAllControllers(this);
            
            sw.Stop();
        }

        public bool IsTemporaryAsset(UnityEngine.Object obj)
        {
            return !EditorUtility.IsPersistent(obj) 
                   || AssetDatabase.GetAssetPath(obj) == AssetDatabase.GetAssetPath(AssetContainer);
        }

        public void Serialize()
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(AssetContainer)))
            {
                return; // unit tests with no serialized assets
            }

            HashSet<UnityObject> _savedObjects =
                new HashSet<UnityObject>(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(AssetContainer)));

            _savedObjects.Remove(AssetContainer);

            int index = 0;
            foreach (var asset in _avatarRootObject.ReferencedAssets(traverseSaved: true, includeScene: false))
            {
                if (asset is MonoScript)
                {
                    // MonoScripts aren't considered to be a Main or Sub-asset, but they can't be added to asset
                    // containers either.
                    continue;
                }
                
                if (_savedObjects.Contains(asset))
                {
                    _savedObjects.Remove(asset);
                    continue;
                }

                if (asset == null)
                {
                    Debug.Log($"Asset {index} is null");
                }
                index++;

                if (!EditorUtility.IsPersistent(asset))
                {
                    try
                    {
                        AssetDatabase.AddObjectToAsset(asset, AssetContainer);
                    }
                    catch (UnityException ex)
                    {
                        Debug.Log($"Error adding asset {asset} p={AssetDatabase.GetAssetOrScenePath(asset)} isMain={AssetDatabase.IsMainAsset(asset)} " +
                                  $"isSub={AssetDatabase.IsSubAsset(asset)} isForeign={AssetDatabase.IsForeignAsset(asset)} isNative={AssetDatabase.IsNativeAsset(asset)}");
                        throw ex;
                    }
                }
            }
            
            // Remove obsolete temporary assets
            foreach (var asset in _savedObjects)
            {
                if (!(asset is Component || asset is GameObject))
                {
                    // Traversal can't currently handle prefabs, so this must have been manually added. Avoid purging it.
                    continue;
                }
                AssetDatabase.RemoveObjectFromAsset(asset);
            }
        }

        public void DeactivateExtensionContext<T>() where T : IExtensionContext
        {
            DeactivateExtensionContext(typeof(T));
        }
        
        public void DeactivateExtensionContext(Type t) {
            if (_activeExtensions.ContainsKey(t))
            {
                var ctx = _activeExtensions[t];
                ctx.OnDeactivate(this);
                _activeExtensions.Remove(t);
            }
        }
        
        internal void RunPass(ConcretePass pass)
        {
            sw.Start();
            
            ImmutableDictionary<Type, double> deactivationTimes = ImmutableDictionary<Type, double>.Empty;
            
            foreach (var ty in pass.DeactivatePlugins)
            {
                Stopwatch sw2 = new Stopwatch();
                sw2.Start();
                DeactivateExtensionContext(ty);
                deactivationTimes = deactivationTimes.Add(ty, sw2.ElapsedMilliseconds);
            }

            ImmutableDictionary<Type, double> activationTimes = ImmutableDictionary<Type, double>.Empty;
            foreach (var ty in pass.ActivatePlugins)
            {
                Stopwatch sw2 = new Stopwatch();
                sw2.Start();
                ActivateExtensionContext(ty);
                activationTimes = activationTimes.Add(ty, sw2.ElapsedMilliseconds);
            }

            Stopwatch passTimer = new Stopwatch();
            passTimer.Start();
            pass.Process(this);
            passTimer.Stop();
            
            BuildEvent.Dispatch(new BuildEvent.PassExecuted(
                pass.InstantiatedPass.QualifiedName,
                passTimer.ElapsedMilliseconds,
                activationTimes,
                deactivationTimes
                ));
            
            sw.Stop();
        }
        
        public T ActivateExtensionContext<T>() where T : IExtensionContext
        {
            return (T) ActivateExtensionContext(typeof(T));
        }

        public IExtensionContext ActivateExtensionContext(Type ty)
        {
            if (!_extensions.TryGetValue(ty, out var ctx))
            {
                ctx = (IExtensionContext) ty.GetConstructor(Type.EmptyTypes).Invoke(Array.Empty<object>());
            }

            if (!_activeExtensions.ContainsKey(ty))
            {
                ctx.OnActivate(this);
                _activeExtensions.Add(ty, ctx);
            }

            return _activeExtensions[ty];
        }

        internal void Finish()
        {
            sw.Start();
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
            sw.Stop();
            
            BuildEvent.Dispatch(new BuildEvent.BuildEnded(sw.ElapsedMilliseconds, true));
        }
    }
}