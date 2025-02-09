using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_DMS
    {
        static Patches_DMS()
        {
            if (ModsConfig.IsActive("Aoba.DeadManSwitch.AncientCorps"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_DMSAncientCorps");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DMSAncientCorps")]
    [HarmonyPatch("AncientCorps.CompAbilityEffect_ActiveProtectionSystem", "AICanTargetNow")]
    public static class Patch_CompAbilityEffect_ActiveProtectionSystem_AICanTargetNow
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap)
                .MethodReplacer(MethodInfoCache.m_OccupiedRect, MethodInfoCache.m_MovedOccupiedRect);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DMSAncientCorps")]
    [HarmonyPatch("AncientCorps.CompAbilityEffect_ActiveProtectionSystem", "CompTick")]
    public static class Patch_CompAbilityEffect_ActiveProtectionSystem_CompTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_OccupiedRect, MethodInfoCache.m_MovedOccupiedRect);
        }
    }
}
