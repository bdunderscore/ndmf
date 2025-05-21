#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using nadena.dev.ndmf.runtime;
using nadena.dev.ndmf.runtime.components;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    /// <summary>
    /// Tracks the set of known platform providers.
    /// </summary>
    public static class PlatformRegistry
    {
        private static ImmutableDictionary<string, INDMFPlatformProvider> _platformProviders
            = ImmutableDictionary<string, INDMFPlatformProvider>.Empty;

        [InitializeOnLoadMethod]
        static void Init()
        {
            var providers = new Dictionary<string, INDMFPlatformProvider>();

            foreach (var type in TypeCache.GetTypesWithAttribute<NDMFPlatformProvider>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;

                if (!typeof(INDMFPlatformProvider).IsAssignableFrom(type))
                    continue;

                INDMFPlatformProvider instance;
                try
                {
                    var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

                    if (instanceProp != null)
                    {
                        instance = (INDMFPlatformProvider)instanceProp.GetValue(null)!;
                    } else {
                        instance = (INDMFPlatformProvider)type.GetConstructor(new Type[0])?.Invoke(null)!;
                    }

                    if (instance.AvatarRootComponentType != null)
                    {
                        RuntimeUtil.AllRootTypes.Add(instance.AvatarRootComponentType);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Initializing platform provider " + type + " failed");
                    Debug.LogException(e);
                    continue;
                }

                if (providers.TryGetValue(instance.QualifiedName, out var existing))
                {
                    Debug.LogError("Multiple platform providers with the same canonical name: " +
                                   instance.QualifiedName + " including " + type + " and " + existing.GetType());
                    continue;
                }

                providers[instance.QualifiedName] = instance;
            }

            _platformProviders = providers.ToImmutableDictionary(
                kv => kv.Key,
                kv => kv.Value
            );
        }

        /// <summary>
        /// Returns a dictionary from qualified name to platform provider.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public static ImmutableDictionary<string, INDMFPlatformProvider> PlatformProviders
        {
            get
            {
                if (_platformProviders.IsEmpty)
                    throw new Exception("Cannot be used in static initializers");
                return _platformProviders;
            }
        }

        public static INDMFPlatformProvider? GetPrimaryPlatformForAvatar(GameObject avatarObject)
        {
            GameObject? cursor = avatarObject;
            var candidateObjects = new HashSet<GameObject>();
            var platforms = new List<INDMFPlatformProvider>();

            while (cursor != null)
            {
                foreach (var provider in PlatformProviders.Values)
                {
                    if (provider.AvatarRootComponentType == null) continue;
                    if (cursor.GetComponent(provider.AvatarRootComponentType) != null)
                    {
                        platforms.Add(provider);
                        candidateObjects.Add(cursor);
                    }
                }
                
                cursor = cursor.transform.parent?.gameObject;
            }

            if (candidateObjects.Count > 1)
            {
                throw new Exception("Multiple avatar roots found in hierarchy.");
            }

            if (candidateObjects.Count > 1)
            {
                if (platforms.Contains(GenericPlatform.Instance)) return GenericPlatform.Instance;
                throw new Exception("Multiple platform providers found for avatar root: " +
                                string.Join(", ", platforms));
            }
            
            return platforms.FirstOrDefault();
        }
    }
}