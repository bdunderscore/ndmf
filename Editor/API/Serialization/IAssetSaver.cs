#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// This interface allows you to explicitly save temporary assets. This can be useful when writing textures, or when
    /// you need to save assets (e.g. animator objects) prior to the automatic serialization pass at the end of processing.
    ///
    /// The asset saver must be disposed at the end of the avatar build. 
    /// </summary>
    public interface IAssetSaver: IDisposable
    {
        /// <summary>
        /// Saves an asset immediately. If the asset is already persistent or is null, this function does nothing.
        /// </summary>
        /// <param name="asset">The asset to save.</param>
        void SaveAsset(UnityEngine.Object? asset);
        /// <summary>
        /// Determines if an asset is temporary and safe to overwrite. Returns true for null.
        /// </summary>
        /// <param name="asset">The asset to check.</param>
        /// <returns>true if the object is non-persistent, or was saved as part of this avatar's processing</returns>
        bool IsTemporaryAsset(UnityEngine.Object? asset);
        /// <summary>
        /// Returns the current unity object which is being used as a container for assets. May return null if asset
        /// saving is disabled.
        ///
        /// Normally, it's better to use SaveAsset; saving too many assets to the same asset container can be slow.
        /// However this property can be used for compatibility with legacy NDMF APIs. 
        /// </summary>
        UnityEngine.Object? CurrentContainer { get; }
        
        /// <summary>
        /// Returns all assets persisted using this IAssetSaver.
        /// </summary>
        /// <returns>an enumerable of assets</returns>
        IEnumerable<UnityEngine.Object> GetPersistedAssets();

        /// <summary>
        /// Saves a list of assets in batch. This can be more efficient than saving assets one by one.
        /// </summary>
        /// <param name="assets"></param>
        void SaveAssets(IEnumerable<UnityEngine.Object> assets)
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var asset in assets)
                {
                    SaveAsset(asset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}