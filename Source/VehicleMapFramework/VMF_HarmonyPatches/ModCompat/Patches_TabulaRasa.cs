﻿using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches.TR;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_TabulaRasa
{
    public const string Category = "VMF_Patches_TabulaRasa";

    static Patches_TabulaRasa()
    {
        if (ModCompat.TabulaRasa)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_TabulaRasa");
        }
    }
}

[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch("TabulaRasa.Comp_Shield", "CurShieldPosition", MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Comp_Shield_CurShieldPosition
{
    public static void Postfix(ThingWithComps ___parent, ref Vector3 __result)
    {
        if (___parent.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            __result = __result.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch("TabulaRasa.Comp_Shield", "ShouldBeBlocked")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_Shield_ShouldBeBlocked
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch("TabulaRasa.Comp_Shield", "BombardmentCanStartFireAt")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_Shield_BombardmentCanStartFireAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch("TabulaRasa.Patch_Projectile_CheckForFreeInterceptBetween", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Patch_Projectile_CheckForFreeInterceptBetween_Postfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map,
            AccessTools.Method(typeof(Patch_Patch_Projectile_CheckForFreeInterceptBetween_Postfix), nameof(ReplaceMap)));
    }

    private static Map ReplaceMap(Projectile instance)
    {
        return Patch_Projectile_CheckForFreeInterceptBetween.tmpMap ?? instance.Map;
    }
}

[HarmonyAfter("Neronix17.TabulaRasa.RimWorld")]
[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch(typeof(Projectile), "CheckForFreeInterceptBetween")]
[PatchLevel(Level.Safe)]
public static class Patch_Projectile_CheckForFreeInterceptBetween
{
    public static void Postfix(Projectile __instance, ref bool __result, Vector3 lastExactPos, Vector3 newExactPos)
    {
        if (!__result)
        {
            try
            {
                var map = __instance.Map;
                var maps = map.BaseMapAndVehicleMaps().Except(map);
                foreach (var map2 in maps)
                {
                    tmpMap = map2;
                    CheckIntercept(null, __instance, __result, lastExactPos, newExactPos);
                    if (__result) break;
                }
            }
            finally
            {
                tmpMap = null;
            }
        }
    }

    public static Map tmpMap;

    private static FastInvokeHandler CheckIntercept = MethodInvoker.GetHandler(AccessTools.Method("TabulaRasa.Patch_Projectile_CheckForFreeInterceptBetween:Postfix"));
}

[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch("TabulaRasa.Patch_Skyfaller_Tick", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Patch_Skyfaller_Tick_Prefix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map,
            AccessTools.Method(typeof(Patch_Patch_Skyfaller_Tick_Prefix), nameof(ReplaceMap)));
    }

    private static Map ReplaceMap(Skyfaller instance)
    {
        return Patch_Skyfaller_Tick.tmpMap ?? instance.Map;
    }
}

[HarmonyAfter("Neronix17.TabulaRasa.RimWorld")]
[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch(typeof(Skyfaller), "Tick")]
[PatchLevel(Level.Safe)]
public static class Patch_Skyfaller_Tick
{
    public static bool Prefix(Projectile __instance)
    {
        try
        {
            var map = __instance.Map;
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            foreach (var map2 in maps)
            {
                tmpMap = map2;
                if (!(bool)CheckIntercept(null, __instance)) return false;
            }
            return true;
        }
        finally
        {
            tmpMap = null;
        }
    }

    public static Map tmpMap;

    private static FastInvokeHandler CheckIntercept = MethodInvoker.GetHandler(AccessTools.Method("TabulaRasa.Patch_Skyfaller_Tick:Prefix"));
}

[HarmonyPatchCategory(Patches_TabulaRasa.Category)]
[HarmonyPatch("TabulaRasa.PlaceWorker_ShowShieldRadius", "DrawGhost")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_ShowShieldRadius_DrawGhost
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Call && instruction.OperandIs(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
            {
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1);
            }
        }
    }
}