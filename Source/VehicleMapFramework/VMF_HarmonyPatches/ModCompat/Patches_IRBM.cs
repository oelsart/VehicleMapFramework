using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_IRBM
{
  static Patches_IRBM()
  {
    if (IRBM)
    {
      VMF_Harmony.PatchCategory(PatchCategories.IRBM);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "DoGroundAttackCheck")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CIWS_DoGroundAttackCheck
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "FindBestGroundTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_CIWS_FindBestGroundTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .End()
      .MatchEndBackwards(new CodeMatch(OpCodes.Ret))
      .Insert(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadLocal(0),
        ((Delegate)PostfixWithMaxRange).Method.CallInstruction)
      .InstructionEnumeration();
  }

  private static Thing PostfixWithMaxRange(Thing thing, IAttackTargetSearcher instance, float groundMaxRange)
  {
    foreach (var map in instance.Thing.Map.BaseMapAndVehicleMaps(false))
    {
      var potentialTargetsFor = map.attackTargetsCache.GetPotentialTargetsFor(instance);
      var num = thing is not null ? Vector3.Distance(instance.Thing.DrawPos, thing.DrawPos) : float.MaxValue;
      for (var i = 0; i < potentialTargetsFor.Count; i++)
      {
        var thing2 = potentialTargetsFor[i].Thing;
        if (!thing2.Destroyed && thing2.Spawned && thing2.HostileTo(Faction.OfPlayer))
        {
          if (thing2 is not Pawn or Pawn { Downed: false, Dead: false })
          {
            var num2 = Vector3.Distance(instance.Thing.DrawPos, thing2.DrawPos);
            if (num2 <= groundMaxRange && instance.Thing.CanSee(thing2) && num2 < num)
            {
              num = num2;
              thing = thing2;
            }
          }
        }
      }
    }

    return thing;
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "DoInterceptionCheck")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CIWS_DoInterceptionCheck
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps);
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "IsTargetValid")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CIWS_IsTargetValid
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "IsHeadingForPlayerAssets")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CIWS_IsHeadingForPlayerAssets
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "FindBestTarget")]
public static class Patch_Building_CIWS_FindBestTarget
{
  [PatchLevel(Level.Safe)]
  public static void Postfix(Thing __instance, ref ILoadReferenceable __result)
  {
    if (__result is not null) return;

    var targetMap = __instance.TargetMap;
    try
    {
      foreach (var map in __instance.Map.BaseMapAndVehicleMaps(false))
      {
        __instance.TargetMap = map;
        __result = FindBestTarget(__instance);
        if (__result is not null) return;
      }
    }
    finally
    {
      __instance.TargetMap = targetMap;
    }
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  [PatchLevel(Level.Mandatory)]
  public static ILoadReferenceable FindBestTarget(Thing instance)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.Building_CIWS", "TriggerShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CIWS_TriggerShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.IRBM)]
[HarmonyPatch("IRBM.RadarUtility", "GetActiveRadars")]
[PatchLevel(Level.Cautious)]
public static class Patch_RadarUtility_GetActiveRadars
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var g_IsPlayerHome = AccessTools.PropertyGetter(typeof(Map), nameof(Map.IsPlayerHome));
    var m_IsPlayerHomeOrVehicleMap = ((Delegate)IsPlayerHomeOrVehicleMap).Method;
    return instructions.MethodReplacer(g_IsPlayerHome, m_IsPlayerHomeOrVehicleMap);
  }

  private static bool IsPlayerHomeOrVehicleMap(Map map)
  {
    return map.IsPlayerHome || map.IsVehicleMap;
  }
}
