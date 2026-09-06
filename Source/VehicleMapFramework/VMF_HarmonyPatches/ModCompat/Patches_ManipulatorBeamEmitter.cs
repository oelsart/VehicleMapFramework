using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat.ManipulatorBeamEmitter;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_ManipulatorBeamEmitter
{
  static Patches_ManipulatorBeamEmitter()
  {
    if (ManipulatorBeamEmitter.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.ManipulatorBeamEmitter);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.Building_BeamManipulator", "CanOperate")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_BeamManipulator_CanOperate
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMapOrCaravan_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.Building_BeamManipulator", "ActiveOperatorCount", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_BeamManipulator_ActiveOperatorCount
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.WorkGiver_OperateBeamManipulator", "GetPriority")]
[PatchLevel(Level.Cautious)]
public static class Patch_WorkGiver_OperateBeamManipulator_GetPriority
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "TryFindConstructionTransfer")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamManipulatorUtility_TryFindConstructionTransfer
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMap)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "ScoreConstructionMaterial")]
[PatchLevel(Level.Cautious)]
public static class Patch_BeamManipulatorUtility_ScoreConstructionMaterial
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "TryFindBestStorageCellCore")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_TryFindBestStorageCellCore
{
  private static bool working;
  
  public static void Postfix(Map map, Thing thing, IntVec3 referenceCell, ref IntVec3 destination, ref bool __result)
  {
    if (working) return;
    if (__result)
    {
      thing.TargetMap = map;
      return;
    }
    working = true;
    referenceCell = !referenceCell.IsValid ? thing.PositionOnBaseMap : referenceCell.ToBaseMapCoord(map);
    try
    {
      object box = destination;
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        var referenceCell2 = map2.IsVehicleMapOf(out var vehicle)
          ? referenceCell.ToVehicleMapCoord(vehicle)
          : referenceCell;
        __result = (bool)TryFindBestStorageCellCore(null,
          Params<(object, object, IntVec3, object, object)>.Get((map2, thing, referenceCell2, null, box)));
        if (__result)
        {
          destination = (IntVec3)box;
          thing.TargetMap = map2;
          return;
        }
      }
      thing.RemoveTargetInfo();
    }
    finally
    {
      working = false;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "FillTransferQueue")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_FillTransferQueue
{
  private static bool working;
  
  public static void Postfix(object op, int desiredCount, object destinationQueue,
    HashSet<Thing> excludedThings, HashSet<IntVec3> excludedDestinations,
    HashSet<IntVec3> candidateSeenCellsScratch, List<IntVec3> candidateCellsScratch)
  {
    if (working) return;
    var thing = OperatorThing(op);
    if (thing is not { Spawned: true }) return;
    var map = thing.Map;
    working = true;
    try
    {
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        using var _ = new VirtualTeleporter(thing, map2);
        FillTransferQueue(null,
          Params<(object, int, object, object, object, object, object)>
            .Get((op, desiredCount, destinationQueue, excludedThings, excludedDestinations,
              candidateSeenCellsScratch, candidateCellsScratch)));
      }
    }
    finally
    {
      working = false;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamAutoOperator", "CanReserve")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamAutoOperator_CanReserve
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(new CodeMatch(OpCodes.Stloc_0))
      .Insert(
        CodeInstruction.LoadArgument(1),
        ((Delegate)ReplaceMap).Method.CallInstruction)
      .InstructionEnumeration();
  }

  private static Map ReplaceMap(Map map, Thing thing) => thing.MapHeld ?? map;
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "CanBeamTransferThing")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamManipulatorUtility_CanBeamTransferThing
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_BeamAutoOperator_CanReserve.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_Cooldown
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var type = GenTypes.GetTypeInAnyAssembly("ManipulatorBeam.BeamManipulatorUtility", "ManipulatorBeam");
    yield return AccessTools.Method(type, "IsStorageRetryCoolingDown");
    yield return AccessTools.Method(type, "IsSourceUnavailableCoolingDown");
    yield return AccessTools.Method(type, "MarkStorageRetryCooldown");
    yield return AccessTools.Method(type, "ClearStorageRetryCooldown");
    yield return AccessTools.Method(type, "MarkSourceUnavailableCooldown");
  }

  public static void Prefix(ref Map map, Thing thing) => map = thing.MapHeld ?? map;
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "IsStorageDestinationStillValid")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_IsStorageDestinationStillValid
{
  public static void Prefix(ref Map map, Thing thing, IntVec3 destination)
  {
    if (destination.IsValid && thing.TargetMap is { } targetMap && destination.InBounds(targetMap))
      map = thing.TargetMap;
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "FinishTransfer")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_FinishTransfer
{
  public static void Prefix(object op, Thing carriedThing, ref VirtualTeleporter? __state)
  {
    var thing = OperatorThing(op);
    if (thing is null) return;
    
    var targetMap = carriedThing.TargetMap;
    if (targetMap is not null && thing.Map != targetMap)
    {
      __state = new VirtualTeleporter(thing, targetMap);
    }
  }

  public static void Finalizer(VirtualTeleporter? __state) => __state?.Dispose();
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamChannelUtility", "BeginTransport")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamChannelUtility_BeginTransport
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .End()
      .MatchStartBackwards(CodeMatch.Calls(AccessTools.Method("ManipulatorBeam.BeamManipulatorUtility:WorldPosForCell")))
      .InsertAfter(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(
            GenTypes.GetTypeInAnyAssembly("ManipulatorBeam.BeamChannelRuntime", "ManipulatorBeam"), "activeTransfer"),
        ((Delegate)ToBaseMapWorldPos).Method.CallInstruction)
      .InstructionEnumeration();
  }
  
  private static Vector3 ToBaseMapWorldPos(Vector3 original, object transfer)
  {
    return transfer is not null
      ? original.ToThingBaseMapCoord(thing(transfer)).WithY(AltitudeLayer.MetaOverlays.AltitudeFor())
      : original;
  } 
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamChannelUtility", "AdvanceWarmup")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamChannelUtility_AdvanceWarmup
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_BeamChannelUtility_BeginTransport.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamChannelUtility", "TryAssignTransfer")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamChannelUtility_TryAssignTransfer
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_BeamChannelUtility_BeginTransport.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "WorldPosForTransferDestination")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamManipulatorUtility_WorldPosForTransferDestination
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .End()
      .MatchStartBackwards(CodeMatch.Calls(AccessTools.Method("ManipulatorBeam.BeamManipulatorUtility:WorldPosForCell")))
      .InsertAfter(
        CodeInstruction.LoadArgument(0),
        ((Delegate)ToBaseMapWorldPos).Method.CallInstruction)
      .InstructionEnumeration();
  }
  
  public static Vector3 ToBaseMapWorldPos(Vector3 original, object transfer)
  {
    return transfer is not null && thing(transfer) is { } t
      ? t.TargetMap is { } map
        ? original.ToBaseMapCoord(map).WithY(AltitudeLayer.MetaOverlays.AltitudeFor())
        : t.DrawPos
      : original;
  } 
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamClaimUtility", "ReleaseClaim")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamClaimUtility_ReleaseClaim
{
  public static void Postfix(object transfer)
  {
    if (transfer is not null) thing(transfer).RemoveTargetInfo();
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamClaimUtility", "StoreForThing")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamClaimUtility_StoreForThing
{
  public static void Prefix(Thing thing, ref VirtualTeleporter? __state)
  {
    if (thing.MapHeld != thing.TargetMap)
    {
      __state = new VirtualTeleporter(thing, thing.TargetMap);
    }
  }
  
  public static void Finalizer(VirtualTeleporter? __state) => __state?.Dispose();
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamClaimUtility", "StoreForTransfer")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamClaimUtility_StoreForTransfer
{
  public static void Prefix(object transfer, ref VirtualTeleporter? __state)
  {
    if (thing(transfer) is { } t && t.MapHeld != t.TargetMap)
    {
      __state = new VirtualTeleporter(t, t.TargetMap);
    }
  }
  
  public static void Finalizer(VirtualTeleporter? __state) => __state?.Dispose();
}