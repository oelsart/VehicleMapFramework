using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(JoyGiver), "GetSearchSet")]
[PatchLevel(Level.Safe)]
public static class Patch_JoyGiver_GetSearchSet
{
  private static bool working;
  private static readonly Action<JoyGiver, Pawn, List<Thing>> GetSearchSet =
    AccessTools.MethodDelegate<Action<JoyGiver, Pawn, List<Thing>>>(
      AccessTools.Method(typeof(JoyGiver), "GetSearchSet"));
  
  private static bool Prepare() => VehicleMapFramework.settings is { joyPatches: true };
  
  public static void Postfix(JoyGiver __instance, Pawn pawn, List<Thing> outCandidates)
  {
    if (working) return;

    working = true;
    try
    {
      foreach (var map in pawn.Map.BaseMapAndVehicleMaps(false))
      {
        using var _ = new VirtualTeleporter(pawn, map);
        GetSearchSet(__instance, pawn, outCandidates);
      }
    }
    finally
    {
      working = false;
    }
  }
}

[HarmonyPatch(typeof(JobGiver_GetJoy), "TryGiveJobFromJoyGiverDefDirect")]
[PatchLevel(Level.Safe)]
public static class Patch_JobGiver_GetJoy_TryGiveJobFromJoyGiverDefDirect
{
  private static bool Prepare() => VehicleMapFramework.settings is { joyPatches: true };
  
  public static void Postfix(Pawn pawn, Job __result)
  {
    var thingMap = __result?.targetA.Thing?.MapHeld;
    if (thingMap != null && thingMap != pawn.Map &&
        !__result.targetB.HasThing)
    {
      // CanReserveSittableOrSpotのパッチはTargetMapしか考慮できないため、Jobではなくpawnにセットしておく
      pawn.TargetInfo = new TargetInfo(__result.targetB.Cell, thingMap);
    }
  }
}

[HarmonyPatch(typeof(WatchBuildingUtility), nameof(WatchBuildingUtility.TryFindBestWatchCell))]
[PatchLevel(Level.Safe)]
public static class Patch_WatchBuildingUtility_TryFindBestWatchCell
{
  private static bool Prepare() => VehicleMapFramework.settings is { joyPatches: true };
  
  public static void Prefix(Thing toWatch, Pawn pawn, ref VirtualTeleporter? __state)
  {
    var thingMap = toWatch.MapHeld;
    if (thingMap is not null && thingMap != pawn.Map)
    {
      __state = new VirtualTeleporter(pawn, thingMap);
    }
  }

  public static void Finalizer(VirtualTeleporter? __state) => __state?.Dispose();
}