using System;
using UnityEditor;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// This helper invokes Unity's StartAssetEditing API, and will StopAssetEditing on disposal.
    /// 
    /// It also provides an API to serialize an asset to an asset container; NDMF internally maintains multiple asset
    /// container files to balance between the overhead of creating a new asset container and the overhead of adding
    /// new assets to existing containers.
    /// </summary>
    public sealed class SerializationScope : IDisposable
    {
        private static bool _assetEditing;

        private readonly IAssetSaver _assetSaver;

        private bool _wasEditing;

        internal SerializationScope(IAssetSaver saver)
        {
            _assetSaver = saver;
            _wasEditing = _assetEditing;
            
            if (!_assetEditing)
            {
                _assetEditing = true;
                AssetDatabase.StartAssetEditing();
            }
        }
        
        public void SaveAsset(UnityEngine.Object asset)
        {
            _assetSaver.SaveAsset(asset);
        }
        
        public void Dispose()
        {
            if (!_wasEditing)
            {
                _assetEditing = false;
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}