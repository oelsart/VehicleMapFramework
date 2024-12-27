using HarmonyLib;
using RimWorld;
using System.Collections.Generic;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(Building_Door), "StuckOpen", MethodType.Getter)]
    public static class Patch_Building_Door_StuckOpen
    {
        public static void Postfix(Building_Door __instance, ref bool __result)
        {
            __result = __result && !(__instance is Building_VehicleSlope);
        }
    }

    [HarmonyPatch(typeof(CompInteractable), nameof(CompInteractable.CanInteract))]
    public static class Patch_CompInteractable_CanInteract
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_PositionHeld, MethodInfoCache.m_PositionHeldOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_ReachabilityUtility_CanReach, MethodInfoCache.m_ReachabilityUtilityOnVehicle_CanReach);
        }
    }
}
