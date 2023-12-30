using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.localization
{
    internal static class NDMFLocales
    {
        public static Localizer L = new Localizer(
            "en-us",
            () => new List<LocalizationAsset>()
            {
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(
                    AssetDatabase.GUIDToAssetPath("5cb11a9adc5d7404d8c01d558a5c0af6")
                ),
                AssetDatabase.LoadAssetAtPath<LocalizationAsset>(
                    AssetDatabase.GUIDToAssetPath("87c99a0330751d842a030f1385973541")
                )                
            }
        );
    }
}