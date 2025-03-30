#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    internal class AssetSaver : IAssetSaver
    {
        internal static Action? OnRetryImport;
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
                using var _2 = new ProfilerScope("DeleteFolder");
                AssetDatabase.DeleteAsset(rootPath);
            }

            using var _ = new ProfilerScope("Asset editing");
            try
            {
                AssetDatabase.StartAssetEditing();

                // Ensure directory exists recursively
                var pathParts = subAssetPath.Split('/');
                var currentDir = pathParts[0];

                for (int i = 1; i < pathParts.Length; i++)
                {
                    var nextDir = currentDir + "/" + pathParts[i];
                    if (!AssetDatabase.IsValidFolder(nextDir))
                    {
                        if (Directory.Exists(nextDir))
                        {
                            // Sometimes the asset database can be unaware of a folder on disk, so refresh it.
                            // This is quite expensive, so only do it if necessary.
                            Debug.Log("Force refresh due to " + nextDir);
                            using var _2 = new ProfilerScope("AssetDatabase.Refresh");
                            AssetDatabase.Refresh();
                        }
                        else
                        {
                            using var _2 = new ProfilerScope("CreateFolder");
                            AssetDatabase.CreateFolder(currentDir, pathParts[i]);
                        }
                    }

                    currentDir = nextDir;
                }
                
                var rootAssetPath = AssetDatabase.GenerateUniqueAssetPath(rootPath + "/" + avatarName + ".asset");
                try
                {
                    _rootAsset = ScriptableObject.CreateInstance<GeneratedAssets>();
                    AssetDatabase.CreateAsset(_rootAsset, rootAssetPath);
                }
                catch (UnityException e)
                {
                    // Sometimes this fails with "Global asset import parameters have been changed during import.
                    // Importing is restarted." - in this case, it might actually have been created, so refresh the
                    // asset database and check if it's saved (and if not, recreate it).
                    Debug.Log("Retrying asset creation due to " + e);

                    AssetDatabase.StopAssetEditing();
                    if (File.Exists(rootAssetPath))
                    {
                        AssetDatabase.DeleteAsset(rootAssetPath);
                    }
                    AssetDatabase.Refresh();
                    AssetDatabase.StartAssetEditing();
                    
                    _rootAsset = ScriptableObject.CreateInstance<GeneratedAssets>();
                    AssetDatabase.CreateAsset(_rootAsset, rootAssetPath);
                    
                    OnRetryImport?.Invoke();
                }

                _currentSubContainer = CreateAssetContainer(inAssetEditing: true);
            
                _assetCount = 0;
            }
            finally
            {
                using var _2 = new ProfilerScope("StopAssetEditing");

                AssetDatabase.StopAssetEditing();
            }
        }

        public void SaveAsset(Object? obj)
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

        private SubAssetContainer CreateAssetContainer(string name = "assets", bool inAssetEditing = false)
        {
            var subContainerPath = AssetDatabase.GenerateUniqueAssetPath(subAssetPath + "/" + name + ".asset");
            var subContainer = ScriptableObject.CreateInstance<SubAssetContainer>();
            AssetDatabase.CreateAsset(subContainer, subContainerPath);

            _containers.Add(subContainer);

            return subContainer;
        }

        public bool IsTemporaryAsset(Object? obj)
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

        internal static string FilterAssetName(string assetName, string? fallbackName = null)
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
                assetName = fallbackName ?? Guid.NewGuid().ToString();
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