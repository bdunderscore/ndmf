using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.ndmf.runtime
{
    /// <summary>
    /// This ScriptableObject is used as a "main asset", allowing users to repack the assets into a more sensible
    /// structure. It contains references to all subassets.
    /// </summary>
    [PreferBinarySerialization]
    public class GeneratedAssets : ScriptableObject
    {
        public List<SubAssetContainer> SubAssets = new();
    }
}