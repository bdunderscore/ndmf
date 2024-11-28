using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf
{
    public class NullAssetSaver : IAssetSaver
    {
        public void SaveAsset(Object asset)
        {
            // no-op
        }

        public bool IsTemporaryAsset(Object asset)
        {
            return !AssetDatabase.Contains(asset);
        }

        public Object CurrentContainer => null;
        
        public IEnumerable<Object> GetPersistedAssets()
        {
            yield break;
        }

        public void Dispose()
        {
            // no-op
        }
    }
}