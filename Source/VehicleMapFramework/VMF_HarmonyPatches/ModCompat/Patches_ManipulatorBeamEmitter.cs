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
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "TryFindHaulBatch")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_TryFindHaulBatch
{
  private static bool working;
  
  public static void Postfix(Pawn pawn, Building manipulator, ref object batch, ref bool __result)
  {
    if (__result || working) return;
    var map = pawn.Map;
    working = true;
    try
    {
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        using var _ = new VirtualTeleporter(pawn, map2);
        __result = (bool)TryFindHaulBatch(null,
          Params<(object, object, object)>.Get((pawn, manipulator, batch)));
        if (__result) return;
      }
    }
    finally
    {
      working = false;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "TryFindConstructionTransferForPawn")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BeamManipulatorUtility_TryFindConstructionTransferForPawn
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    CodeMatch[] match = [CodeMatch.Calls(CachedMethodInfo.g_Thing_Map)];
    return new CodeMatcher(instructions)
      .MatchStartForward(match)
      .MatchStartForward(match)
      .MatchStartForward(match).Set(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing)
      .MatchStartForward(match).Set(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMap)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "TryFindBestStorageCellCore")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_TryFindBestStorageCellCore
{
  private static bool working;
  
  public static void Postfix(Map map, Thing thing, ref IntVec3 destination, ref bool __result)
  {
    if (working) return;
    if (__result)
    {
      thing.TargetMap = map;
      return;
    }
    working = true;
    try
    {
      object box = destination;
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        __result = (bool)TryFindBestStorageCellCore(null,
          Params<(object, object, object, object)>.Get((map2, thing, null, box)));
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
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_FillTransferQueue
{
  private static bool working;

  private static MethodBase TargetMethod()
  {
    return AccessTools.FirstMethod(
      GenTypes.GetTypeInAnyAssembly("ManipulatorBeam.BeamManipulatorUtility", "ManipulatorBeam"),
      m => m.Name == "FillTransferQueue" && m.GetParameters().Length >= 9);
  }
  
  public static void Postfix(Pawn pawn, Building manipulator, int desiredCount, object destinationQueue,
    HashSet<Thing> excludedThings, HashSet<IntVec3> excludedDestinations, Thing preferredThing,
    HashSet<IntVec3> candidateSeenCellsScratch, List<IntVec3> candidateCellsScratch)
  {
    if (working) return;
    var map = pawn.Map;
    working = true;
    try
    {
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        using var _ = new VirtualTeleporter(pawn, map2);
        FillTransferQueue(null,
          Params<(object, object, int, object, object, object, object, object, object)>
            .Get((pawn, manipulator, desiredCount, destinationQueue, excludedThings, excludedDestinations, preferredThing,
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
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_FillTransferQueueAuto
{
  private static bool working;

  private static MethodBase TargetMethod()
  {
    return AccessTools.FirstMethod(
      GenTypes.GetTypeInAnyAssembly("ManipulatorBeam.BeamManipulatorUtility", "ManipulatorBeam"),
      m => m.Name == "FillTransferQueueAuto" && m.GetParameters().Length >= 7);
  }
  
  public static void Postfix(Building building, int desiredCount, object destinationQueue,
    HashSet<Thing> excludedThings, HashSet<IntVec3> excludedDestinations, HashSet<IntVec3> candidateSeenCellsScratch,
    List<IntVec3> candidateCellsScratch)
  {
    if (working) return;
    var map = building.Map;
    working = true;
    try
    {
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        using var _ = new VirtualTeleporter(building, map2);
        FillTransferQueueAuto(null,
          Params<(object, int, object, object, object, object, object)>
            .Get((building, desiredCount, destinationQueue, excludedThings, excludedDestinations,
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
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "CanAutoTransferThingForOwner")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_CanAutoTransferThingForOwner
{
  public static void Prefix(Building building, Thing thing, ref VirtualTeleporter? __state)
  {
    if (thing.Spawned && building.Map != thing.Map)
      __state = new VirtualTeleporter(building, thing.Map);
  }
  
  public static void Finalizer(VirtualTeleporter? __state) => __state?.Dispose();
}


[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.BeamManipulatorUtility", "IsStorageDestinationStillValid")]
[PatchLevel(Level.Safe)]
public static class Patch_BeamManipulatorUtility_IsStorageDestinationStillValid
{
  public static void Prefix(ref Map map, Thing thing)
  {
    map = thing.TargetMap ?? map;
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch]
public static class Patch_BeamManipulatorUtility_FinishTransfer
{
  private static Map targetMap;

  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method("ManipulatorBeam.BeamManipulatorUtility:FinishTransfer");
    yield return AccessTools.Method("ManipulatorBeam.BeamManipulatorUtility:FinishTransferAuto");
  } 
  
  [PatchLevel(Level.Safe)]
  public static void Prefix(Thing carriedThing)
  {
    targetMap = carriedThing.TargetMap;
  }

  [PatchLevel(Level.Sensitive)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
  {
    CodeMatch[] match = [new(OpCodes.Ldloc_0), new(OpCodes.Ldarg_0), CodeMatch.Calls(CachedMethodInfo.g_Thing_Map)];
    var m_TargetMap = ((Delegate)TargetMapOrThingMap).Method;
    // fallbackCellを使ってるとこはTargetMap
    return new CodeMatcher(instructions)
      .MatchEndForward(match)
      .Repeat(c => c.Set(OpCodes.Call, m_TargetMap))
      .InstructionEnumeration();
  }
  
  private static Map TargetMapOrThingMap(Thing thing) => targetMap ?? thing.Map;
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
      .MatchStartBackwards(CodeMatch.Calls(WorldPosForCell))
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
      .MatchStartBackwards(CodeMatch.Calls(WorldPosForCell))
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
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobDriver_OperateBeamManipulator_MakeNewToils
{
  private static MethodBase TargetMethod()
  {
    return GenTypes.GetTypeInAnyAssembly("ManipulatorBeam.JobDriver_OperateBeamManipulator", "ManipulatorBeam")
      .FindIncludingInnerTypes(t => t.GetDeclaredMethods().FirstOrDefault(m =>
        m.CallsMethod(WorldPosForCell)));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var f_ActiveTransfer = AccessTools.Field("ManipulatorBeam.BeamChannelRuntime:activeTransfer");
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.LoadsField(f_ActiveTransfer))
      .DeclareLocal(f_ActiveTransfer.FieldType, out var activeTransfer)
      .InsertAfterAndAdvance(
        new CodeInstruction(OpCodes.Dup),
        new CodeInstruction(OpCodes.Stloc_S, activeTransfer))
      .MatchStartForward(CodeMatch.Calls(WorldPosForCell))
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldloc_S, activeTransfer),
        ((Delegate)Patch_BeamManipulatorUtility_WorldPosForTransferDestination.ToBaseMapWorldPos).Method
        .CallInstruction)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.ManipulatorBeamEmitter)]
[HarmonyPatch("ManipulatorBeam.Building_BeamManipulatorAuto", "Tick")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_BeamManipulatorAuto_Tick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return Patch_JobDriver_OperateBeamManipulator_MakeNewToils.Transpiler(instructions, generator);
  }
}