using HarmonyLib;
using System.Collections.Generic;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_DMS
    {
        static Patches_DMS()
        {
            if (ModCompat.DeadMansSwitch)
            {
                VMF_Harmony.PatchCategory("VMF_Patches_DMS");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DMS")]
    [HarmonyPatch("DMS.Verb_CastAbilityArcSprayProjectile", "TryCastShot")]
    public static class Patch_Verb_CastAbilityArcSprayProjectile_TryCastShot
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DMS")]
    [HarmonyPatch("DMS.Verb_CastAbilityArcSprayProjectile", "PreparePath")]
    public static class Patch_Verb_CastAbilityArcSprayProjectile_PreparePath
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Verb_ArcSpray_PreparePath.Transpiler(instructions);
        }
    }
}