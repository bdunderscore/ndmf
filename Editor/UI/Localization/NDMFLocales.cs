using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.localization
{
    internal static class NDMFLocales
    {
        public static Localizer L = new Localizer(
            "en-US",
            () => new List<LocalizationAsset>()
            {
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(
                    AssetDatabase.GUIDToAssetPath("5cb11a9adc5d7404d8c01d558a5c0af6")
                ),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(
                    AssetDatabase.GUIDToAssetPath("87c99a0330751d842a030f1385973541")
                ),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(
                    AssetDatabase.GUIDToAssetPath("b1fe4225ad3686e46bb3257770364b6e")
                )                
            }
        );
    }
}
