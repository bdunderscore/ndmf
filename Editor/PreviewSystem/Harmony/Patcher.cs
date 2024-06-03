#region

using System;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal sealed class PatchLoader
    {
        private static readonly Action<Harmony>[] patches =
        {
            HandleUtilityPatches.Patch_FilterInstanceIDs,
            PickingObjectPatch.Patch,
            HierarchyViewPatches.Patch
        };

        [InitializeOnLoadMethod]
        static void ApplyPatches()
        {
            var harmony = new Harmony("nadena.dev.ndmf.core.preview");

            foreach (var patch in patches)
            {
                try
                {
                    patch(harmony);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            AssemblyReloadEvents.beforeAssemblyReload += () => { harmony.UnpatchAll(); };
        }
    }
}