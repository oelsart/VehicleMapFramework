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
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
    public static class Patch_FloatMenuMakerMap_ChoicesAtFor
    {
        public static bool Prefix(Vector3 clickPos, Pawn pawn, bool suppressAutoTakeableGoto, ref List<FloatMenuOption> __result)
        {
            var vehicles = VehiclePawnWithMapCache.allVehicles[Find.CurrentMap];
            var vehicle = vehicles.FirstOrDefault(v =>
            {
                var rect = new Rect(0f, 0f, (float)v.interiorMap.Size.x, (float)v.interiorMap.Size.z);
                var vector = clickPos.VehicleMapToOrig(v);

                return rect.Contains(new Vector2(vector.x, vector.z));
            });
            if (vehicle != null || pawn.IsOnVehicleMapOf(out _))
            {
                SelectorOnVehicleUtility.vehicleForSelector = vehicle;
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
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(List<Pawn>), typeof(IntVec3))]
        public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }
}
