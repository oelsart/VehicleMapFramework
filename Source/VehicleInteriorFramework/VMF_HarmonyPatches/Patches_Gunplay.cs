using HarmonyLib;
using System.Collections.Generic;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_Gunplay
    {
        static Patches_Gunplay()
        {
            if (ModCompat.Gunplay)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_Gunplay");

                VMF_Harmony.Instance.Patch(AccessTools.Method("Gunplay.Patch.PatchProjectileLaunch:Postfix"), transpiler: AccessTools.Method(typeof(Patch_PatchProjectileLaunch_Postfix), nameof(Patch_PatchProjectileLaunch_Postfix.Transpiler)));
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_Gunplay")]
    [HarmonyPatch("Gunplay.Patch.PatchProjectileLaunch", "Postfix")]
    public static class Patch_PatchProjectileLaunch_Postfix
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }
}
