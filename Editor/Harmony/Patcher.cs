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
        private const string HarmonyId = "nadena.dev.ndmf.core.preview";

        private static readonly Action<Harmony>[] patches =
        {
            HandleUtilityPatches.Patch_FilterInstanceIDs,
            PickingObjectPatch.Patch,
            VRCSDKBug_AssemblyGetNameExceptionHandling.Patch
            //HierarchyViewPatches.Patch
        };

        [InitializeOnLoadMethod]
        static void ApplyPatches()
        {
            var harmony = new Harmony(HarmonyId);

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

            AssemblyReloadEvents.beforeAssemblyReload += () => { harmony.UnpatchAll(HarmonyId); };
        }
    }
}