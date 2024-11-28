using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf
{
    public class SingleAssetSaver : IAssetSaver
    {
        private readonly UnityEngine.Object _container;
        
        public SingleAssetSaver(UnityEngine.Object container)
        {
            _container = container;
        }
        
        public void SaveAsset(UnityEngine.Object obj)
        {
            if (AssetDatabase.Contains(obj)) return;
            AssetDatabase.AddObjectToAsset(obj, _container);
        }

        public bool IsTemporaryAsset(Object asset)
        {
            return AssetDatabase.GetAssetPath(asset) == AssetDatabase.GetAssetPath(_container);
        }

        public Object CurrentContainer => _container;
        
        public IEnumerable<Object> GetPersistedAssets()
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_container));
        }

        public void Dispose()
        {
            AssetDatabase.SaveAssets();
        }
    }
}