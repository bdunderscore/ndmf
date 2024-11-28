#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    internal class AssetSaver : IAssetSaver
    {
        private readonly string subAssetPath, rootAssetPath;
        private readonly int assetsPerContainer;
        
        private GeneratedAssets _rootAsset;
        private SubAssetContainer _currentSubContainer;
        private int _assetCount;
        private HashSet<Object> _temporaryAssets = new HashSet<Object>();
        private List<SubAssetContainer> _containers = new();

        public Object CurrentContainer => _currentSubContainer;

        internal AssetSaver(string generatedAssetsRoot, string avatarName, int assetsPerContainer = 256)
        {
            this.assetsPerContainer = assetsPerContainer;

            avatarName = FilterAssetName(avatarName, "avatar");
            var rootPath = generatedAssetsRoot + "/" + avatarName;
            subAssetPath = rootPath + "/_assets";

            this.rootAssetPath = rootPath + "/";

            if (AssetDatabase.IsValidFolder(subAssetPath))
            {
                AssetDatabase.DeleteAsset(rootPath);
            }
            
            // Ensure directory exists recursively
            if (!AssetDatabase.IsValidFolder(subAssetPath))
            {
                var parts = subAssetPath.Split('/');
                var currentPath = parts[0];
                
                for (var i = 1; i < parts.Length; i++)
                {
                    if (!AssetDatabase.IsValidFolder(currentPath + "/" + parts[i]))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }

                    currentPath += "/" + parts[i];
                }
            }
            
            var rootAssetPath = AssetDatabase.GenerateUniqueAssetPath(rootPath + "/" + avatarName + ".asset");
            _rootAsset = ScriptableObject.CreateInstance<GeneratedAssets>();
            AssetDatabase.CreateAsset(_rootAsset, rootAssetPath);
            _currentSubContainer = CreateAssetContainer();
            
            _assetCount = 0;
        }

        public void SaveAsset(UnityEngine.Object? obj)
        {
            if (obj == null || EditorUtility.IsPersistent(obj)) return;

            _temporaryAssets.Add(obj);
            
            if (obj is Texture)
            {
                // Textures can be quite large, so push them off to their own files.
                // However, be sure to create them as a subasset, as this appears to be much faster, for some reason...
                var texName = FilterAssetName(obj.name, "texture");
                var container = CreateAssetContainer(texName);
                
                AssetDatabase.AddObjectToAsset(obj, container);
                return;
            }
            
            if (_assetCount >= assetsPerContainer)
            {
                _currentSubContainer = CreateAssetContainer();
                _assetCount = 0;
            }
            
            AssetDatabase.AddObjectToAsset(obj, _currentSubContainer);
            _assetCount++;
        }

        private SubAssetContainer CreateAssetContainer(string name = "assets")
        {
            var subContainerPath = AssetDatabase.GenerateUniqueAssetPath(subAssetPath + "/" + name + ".asset");
            var subContainer = ScriptableObject.CreateInstance<SubAssetContainer>();
            AssetDatabase.CreateAsset(subContainer, subContainerPath);
            
            _containers.Add(subContainer);

            return subContainer;
        }
        
        public bool IsTemporaryAsset(UnityEngine.Object? obj)
        {
            if (obj == null) return true;
            
            if (_temporaryAssets.Contains(obj) || !EditorUtility.IsPersistent(obj))
            {
                return true;
            }
            
            var path = AssetDatabase.GetAssetPath(obj);
            if (path.StartsWith(rootAssetPath))
            {
                _temporaryAssets.Add(obj);
                return true;
            }

            return false;
        }


        private static readonly Regex WindowsReservedFileNames = new Regex(
            "(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])([.].*)?",
            RegexOptions.IgnoreCase
        );

        private static readonly Regex WindowsReservedFileCharacters = new Regex(
            "[<>:\"/\\\\|?*\x00-\x1f]",
            RegexOptions.IgnoreCase
        );

        private static readonly Regex StripLeadingTrailingWhitespace = new Regex(
            "^[\\s]*((?=\\S).*\\S)[\\s]*$"
        );

        internal static string FilterAssetName(string assetName, string fallbackName)
        {
            assetName = WindowsReservedFileCharacters.Replace(assetName, "_");

            if (WindowsReservedFileNames.IsMatch(assetName))
            {
                assetName = "_" + assetName;
            }

            var match = StripLeadingTrailingWhitespace.Match(assetName);
            if (match.Success)
            {
                assetName = match.Groups[1].Value;
            } else {
                assetName = Guid.NewGuid().ToString();
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                assetName = fallbackName;
            }

            return assetName;
        }
        
        
        public IEnumerable<Object> GetPersistedAssets()
        {
            return _temporaryAssets.Where(o => o != null);
        }

        public void Dispose()
        {
            _rootAsset.SubAssets = _containers;
            EditorUtility.SetDirty(_rootAsset);
            AssetDatabase.SaveAssets();
        }
    }
}