using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
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
            if (clickPos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false) || pawn.IsOnNonFocusedVehicleMapOf(out _))
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
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(FloatMenuOption), nameof(FloatMenuOption.revalidateClickTarget)),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_ToThingBaseMapCoord2)
            });
            return codes;
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

    //ValidateTakeToBedOptionの完全な置き換え(FindBedとReservation関係が置換してある)。こんなところはあまり触られてないことを祈る
    [HarmonyPatch(typeof(FloatMenuMakerMap), "ValidateTakeToBedOption")]
    public static class Patch_FloatMenuMakerMap_ValidateTakeToBedOption
    {
        public static bool Prefix(Pawn pawn, Pawn target, FloatMenuOption option, string cannot, GuestStatus? guestStatus)
        {
            FloatMenuMakerOnVehicle.ValidateTakeToBedOption(pawn, target, option, cannot, guestStatus);
            return false;
        }
    }
}
