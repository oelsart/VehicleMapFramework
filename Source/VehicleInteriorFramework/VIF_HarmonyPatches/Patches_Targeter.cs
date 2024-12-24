using HarmonyLib;
using RimWorld;
using System.Collections.Generic;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(Targeter), "ConfirmStillValid")]
    public static class Patch_Targeter_ConfirmStillValid
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }

    [HarmonyPatch(typeof(Targeter), "OrderVerbForceTarget")]
    public static class Patch_Targeter_OrderVerbForceTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(Targeter), "CurrentTargetUnderMouse")]
    public static class Patch_Targeter_CurrentTargetUnderMouse
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_GenUI_TargetsAtMouse, MethodInfoCache.m_GenUIOnVehicle_TargetsAtMouse);
        }
    }
}