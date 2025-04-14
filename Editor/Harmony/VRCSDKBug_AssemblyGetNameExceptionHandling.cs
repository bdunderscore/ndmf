using System;
using HarmonyLib;

namespace nadena.dev.ndmf.preview
{
    internal static class VRCSDKBug_AssemblyGetNameExceptionHandling
    {
        internal static void Patch(Harmony h)
        {
            var t_Tools = AccessTools.TypeByName("VRC.Tools");
            var p_HasTypeVRCApplication = AccessTools.Property(t_Tools, "HasTypeVRCApplication");

            try
            {
                h.Patch(p_HasTypeVRCApplication.GetMethod,
                    new HarmonyMethod(typeof(VRCSDKBug_AssemblyGetNameExceptionHandling), nameof(AlwaysFalse)));
            }
            catch (NullReferenceException)
            {
                // ignore - VRCSDK has patched the issue already
            }
        }

        private static bool AlwaysFalse(ref bool __result)
        {
            __result = false;

            return false;
        }
    }
}