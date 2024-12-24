using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.TryMakeFloatMenu))]
    public static class Patch_FloatMenuMakerMap_TryMakeFloatMenu
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
    public static class Patch_FloatMenuMakerMap_ChoicesAtFor
    {
        public static bool Prefix(Vector3 clickPos, Pawn pawn, bool suppressAutoTakeableGoto, ref List<FloatMenuOption> __result)
        {
            if (clickPos.TryGetVehiclePawnWithMap(out var vehicle) || pawn.IsOnVehicleMapOf(out _))
            {
                GenUIOnVehicle.vehicleForSelector = vehicle;
                __result = FloatMenuMakerOnVehicle.ChoicesAtFor(clickPos, pawn, suppressAutoTakeableGoto);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(FloatMenuMap), "StillValid")]
    public static class Patch_FloatMenuMap_StillValid
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch]
    public static class Patch_EnterPortalUtility_GetFloatMenuOptFor
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(Pawn), typeof(IntVec3))]
        public static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(List<Pawn>), typeof(IntVec3))]
        public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }
}
