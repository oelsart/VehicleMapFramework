using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(UI), nameof(UI.MouseCell))]
[PatchLevel(Level.Sensitive)]
public static class Patch_UI_MouseCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_ToIntVec3));
    codes.Insert(pos, CachedMethodInfo.m_ToVehicleMapCoord.CallInstruction);
    return codes;
  }

  public static IntVec3 MouseCell()
  {
    return UI.UIToMapPosition(UI.MousePositionOnUI).ToIntVec3();
  }
}

[HarmonyPatch(typeof(GenThing), nameof(GenThing.TrueCenter))]
public static class Patch_GenThing_TrueCenter
{
  private static bool skipFlag;

  [HarmonyBefore(VehicleFramework.HarmonyId)]
  [HarmonyPatch([typeof(Thing)])]
  [PatchLevel(Level.Mandatory)]
  public static bool Prefix(Thing t, ref Vector3 __result)
  {
    if (!t.TryGetDrawPos(ref __result))
    {
      skipFlag = true;
      return true;
    }

    return false;
  }

  [HarmonyPatch([typeof(Thing)])]
  [PatchLevel(Level.Mandatory)]
  public static void Finalizer()
  {
    skipFlag = false;
  }

  [HarmonyPatch([typeof(IntVec3), typeof(Rot4), typeof(IntVec2), typeof(float)])]
  [PatchLevel(Level.Safe)]
  public static void Postfix(ref Vector3 __result)
  {
    // TrueCenter(this Thing t)から呼ばれた場合はオフセットしない
    if (skipFlag)
    {
      return;
    }

    if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle) &&
        !VehicleSectionLayerManager.CacheMode && !VehiclePawnWithMapCache.CacheMode)
    {
      __result = __result.ToBaseMapCoord(vehicle);
      __result.y = Mathf.Min(__result.y, AltitudeLayer.MetaOverlays.AltitudeFor());
    }
  }
}

[HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Pawn_DrawTracker_DrawPos
{
  public static bool Prefix(Pawn ___pawn, ref Vector3 __result)
  {
    return !___pawn.TryGetDrawPos(ref __result);
  }

  public static void Postfix(Pawn ___pawn, ref Vector3 __result)
  {
    __result.y += ___pawn.jobs?.curDriver is IBodyOffsetJobDriver driver ? driver.PawnDrawPosOffset_Y : 0f;
  }
}

[HarmonyPatch(typeof(VehicleDrawTracker), nameof(VehicleDrawTracker.DrawPos), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_VehiclePawn_DrawPos
{
  public static bool Prefix(VehiclePawn ___vehicle, ref Vector3 __result, out bool __state)
  {
    __state = !___vehicle.TryGetDrawPos(ref __result);
    return __state;
  }

  public static void Postfix(VehiclePawn ___vehicle, ref Vector3 __result, bool __state)
  {
    if (__state)
    {
      __result += ___vehicle.jobs?.curDriver is JobDriverBodyOffset driver ? driver.ForcedBodyOffset : Vector3.zero;
    }
  }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.DrawPos), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Projectile_DrawPos
{
  public static bool Prefix(Projectile __instance, ref Vector3 __result)
  {
    return !__instance.TryGetDrawPos(ref __result);
  }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.ExactRotation), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Projectile_ExactRotation
{
  public static void Postfix(Projectile __instance, ref Quaternion __result)
  {
    if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      __result *= vehicle.FullAngleQuat;
    }
  }
}

[HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.InViewOf))]
[PatchLevel(Level.Cautious)]
public static class Patch_CameraDriver_InViewOf
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap,
      CachedMethodInfo.m_ClipInsideVehicleMap);
  }
}

[HarmonyPatch(typeof(Mote), nameof(Mote.DrawPos), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Mote_DrawPos
{
  public static bool Prefix(Mote __instance, ref Vector3 __result)
  {
    if (__instance.link1.Target.HasThing) return true;

    return !__instance.TryGetDrawPos(ref __result);
  }
}

[HarmonyPatch(typeof(MoteAttached), "TimeInterval")]
[PatchLevel(Level.Sensitive)]
public static class Patch_MoteAttached_TimeInterval
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_ToIntVec3))
      .Insert(
        CodeInstruction.LoadArgument(0),
        CachedMethodInfo.m_ToThingMapCoord.CallInstruction)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(VehicleSkyfaller), "RootPos", MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleSkyfaller_RootPos
{
  public static void Postfix(VehicleSkyfaller __instance, ref Vector3 __result)
  {
    if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      __result = __result.ToBaseMapCoord(vehicle);
    }
  }
}

[HarmonyPatch(typeof(FleckSystemBase<FleckStatic>), nameof(FleckSystemBase<>.CreateFleck))]
[PatchLevel(Level.Safe)]
public static class Patch_FleckSystemBase_FleckStatic_CreateFleck
{
  public static void Prefix(FleckSystemBase<FleckStatic> __instance, ref FleckCreationData creationData)
  {
    if (__instance.parent.parent.IsNonFocusedVehicleMapOf(out var vehicle))
    {
      creationData.spawnPosition = creationData.spawnPosition.ToBaseMapCoord(vehicle);
    }
  }
}

[HarmonyPatch(typeof(FleckSystemBase<FleckThrown>), nameof(FleckSystemBase<>.CreateFleck))]
[PatchLevel(Level.Safe)]
public static class Patch_FleckSystemBase_FleckThrown_CreateFleck
{
  public static void Prefix(FleckSystemBase<FleckThrown> __instance, ref FleckCreationData creationData)
  {
    if (__instance.parent.parent.IsNonFocusedVehicleMapOf(out var vehicle))
    {
      creationData.spawnPosition = creationData.spawnPosition.ToBaseMapCoord(vehicle);
    }
  }
}

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.DrawLinesBetweenTargets))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Pawn_JobTracker_DrawLinesBetweenTargets
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
  {
    var g_CenterVector3 = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3));
    var match = CodeMatch.Calls(g_CenterVector3);
    var local_i = original.GetMethodBody()?.LocalVariables
      .FirstOrDefault(l => l.LocalType == typeof(int));
    var i_index = local_i?.LocalIndex ?? 3;
    var g_Item = AccessTools.Method(typeof(JobQueue), "get_Item");
    
    return PatchHelper.CreateCodeMatcherFast(instructions)
      // pawn.Position.ToVector3Shifted().ToThingBaseMapCoord(pawn);
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
      .InsertAfterAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn"),
        CachedMethodInfo.m_ToThingBaseMapCoord.CallInstruction)
      
      // pawn.pather.Destination.CenterVector3VehicleOffsetPawn(pawn);
      .MatchStartForward(match)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn"))
      .SetOperandAndAdvance(((Delegate)CenterVector3VehicleOffsetPawn).Method)
      
      // curJob.targetA.CenterVector3VehicleOffsetJob(curJob);
      .MatchStartForward(match)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn"),
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.curJob)))
      .SetOperandAndAdvance(((Delegate)CenterVector3VehicleOffsetJob).Method)
      .MatchStartForward(match)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn"),
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.curJob)))
      .SetOperandAndAdvance(((Delegate)CenterVector3VehicleOffsetJob).Method)
      
      // jobQueue[i].job.targetA.CenterVector3VehicleOffsetJob(jobQueue[i].job);
      // targetQueueA[j].CenterVector3VehicleOffsetJob(jobQueue[i].job);
      .MatchStartForward(match)
      .Repeat(c => c
        .InsertAndAdvance(
          CodeInstruction.LoadArgument(0),
          CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn"),
          CodeInstruction.LoadArgument(0),
          CodeInstruction.LoadField(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.jobQueue)),
          CodeInstruction.LoadLocal(i_index),
          g_Item.CallvirtInstruction,
          CodeInstruction.LoadField(typeof(QueuedJob), nameof(QueuedJob.job)))
        .SetOperandAndAdvance(((Delegate)CenterVector3VehicleOffsetJob).Method))
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }

  public static Vector3 CenterVector3VehicleOffsetPawn(ref LocalTargetInfo targ, Pawn pawn)
  {
    var map = pawn.MapHeld ?? Find.CurrentMap;
    return CenterVector3VehicleOffset(ref targ, map);
  }

  public static Vector3 CenterVector3VehicleOffsetJob(ref LocalTargetInfo targ, Pawn pawn, Job job)
  {
    var map = job?.globalTarget.Map ?? pawn.MapHeld ?? Find.CurrentMap;
    return CenterVector3VehicleOffset(ref targ, map);
  }
  
  public static Vector3 CenterVector3VehicleOffset(ref LocalTargetInfo targ, Map map)
  {
    if (targ.HasThing)
    {
      if (targ.Thing.Spawned)
      {
        return targ.Thing.DrawPos;
      }

      return targ.Thing.SpawnedOrAnyParentSpawned
        ? targ.Thing.SpawnedParentOrMe.DrawPos
        : targ.Thing.Position.ToVector3Shifted().ToBaseMapCoord(map);
    }

    return targ.Cell.IsValid ? targ.Cell.ToVector3Shifted().ToBaseMapCoord(map) : default;
  }
}

[HarmonyPatch(typeof(PawnPath), nameof(PawnPath.DrawPath))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PawnPath_DrawPath
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .NonFocusedMapVehicle(out var vehicle, CodeInstruction.LoadArgument(1))
      .AddAltitudeFor(vehicle)
      .MatchEndForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted), CodeMatch.IsStloc())
      .Repeat(c => c
        .CreateLabel(out var label2)
        .Insert(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          new CodeInstruction(OpCodes.Brfalse_S, label2),
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_ToBaseMapCoord2.CallInstruction))
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(Designation), nameof(Designation.DrawLoc))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Designation_DrawLoc
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(((Delegate)Vector2Utility.ToVector3).Method))
      .Advance()
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        AccessTools.PropertyGetter(typeof(Designation), "Map").CallvirtInstruction,
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        CachedMethodInfo.m_IsNonFocusedVehicleMapOf.CallInstruction,
        new CodeInstruction(OpCodes.Pop))
      .RotatedByVehicleExtraAngle(vehicle)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3ShiftedWithAltitude1))
      .InsertAfter(CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(OverlayDrawer), "RenderPulsingOverlay",
  typeof(Thing), typeof(Material), typeof(int), typeof(Mesh), typeof(bool))]
[PatchLevel(Level.Sensitive)]
public static class Patch_OverlayDrawer_RenderPulsingOverlay
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(((Delegate)Vector2Utility.ToVector3).Method))
      .Advance()
      .NonFocusedMapVehicle(out var vehicle, CodeInstruction.LoadArgument(1))
      .RotatedByVehicleExtraAngle(vehicle)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(GenDraw), "DrawInteractionCell")]
[PatchLevel(Level.Sensitive)]
public static class Patch_GenDraw_DrawInteractionCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3ShiftedWithAltitude2))
      .InsertAfter(CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
  }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges),
  typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int))]
[PatchLevel(Level.Safe)]
public static class Patch_GenDraw_DrawFieldEdges
{
  public static bool Prefix(List<IntVec3> cells, Color color, float? altOffset,
    HashSet<IntVec3> ignoreBorderCells, int renderQueue)
  {
    if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle))
    {
      GenDrawOnVehicle.DrawFieldEdges(cells, color, altOffset, ignoreBorderCells, renderQueue, vehicle.CurrentLevel);
      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawCircleOutline), typeof(Vector3), typeof(float), typeof(Material))]
[PatchLevel(Level.Safe)]
public static class Patch_GenDraw_DrawCircleOutline
{
  public static void Prefix(ref Vector3 center)
  {
    center = center.ToBaseMapCoord();
  }
}

[HarmonyPatch(typeof(RoyalTitlePermitWorker_CallShuttle), nameof(RoyalTitlePermitWorker_CallShuttle.DrawShuttleGhost))]
[PatchLevel(Level.Sensitive)]
public static class Patch_RoyalTitlePermitWorker_CallShuttle_DrawShuttleGhost
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Quaternion_identity));
    codes.Insert(pos, CachedMethodInfo.m_FocusedDrawPosOffset.CallInstruction);
    return codes;
  }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawTargetHighlightWithLayer))]
public static class Patch_GenDraw_DrawTargetHighlightWithLayer
{
  //Vector3 position = c.ToVector3ShiftedWithAltitude(layer); ->
  //Vector3 position = c.ToVector3ShiftedWithAltitude(layer).OrigToVehicleMap();
  [PatchLevel(Level.Sensitive)]
  [HarmonyPatch([typeof(IntVec3), typeof(AltitudeLayer), typeof(Material)])]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
    codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1));
    return codes;
  }
}

//CellがターゲットのMoteにオフセットをかける
[HarmonyPatch(typeof(MoteAttachLink), nameof(MoteAttachLink.UpdateDrawPos))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MoteAttachLink_UpdateDrawPos
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var pos =
      codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_IntVec3_ToVector3Shifted)) + 1;
    var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
    var label = generator.DefineLabel();

    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadField(typeof(MoteAttachLink), "targetInt", true),
      new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Map))),
      new CodeInstruction(OpCodes.Ldloca, vehicle),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2)
    ]);
    return codes;
  }
}

[HarmonyPatch(typeof(SubEffecter_Sprayer), "MakeMote")]
[PatchLevel(Level.Safe)]
public static class Patch_SubEffecter_Sprayer_MakeMote
{
  public static void Prefix(SubEffecter_Sprayer __instance, TargetInfo A, TargetInfo B)
  {
    var locType = __instance.EffectiveSpawnLocType;
    if (locType == MoteSpawnLocType.OnSource && A.HasThing || locType == MoteSpawnLocType.OnTarget && B.HasThing)
      return;
    VehiclePawnWithMapCache.CacheMode = true;
  }

  public static void Finalizer()
  {
    VehiclePawnWithMapCache.CacheMode = false;
  }
}

[HarmonyPatch(typeof(PlaceWorker_SpectatorPreview), nameof(PlaceWorker_SpectatorPreview.DrawSpectatorPreview))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_SpectatorPreview_DrawSpectatorPreview
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(((Delegate)SpectatorCellFinder.AsRot4).Method))
      .Advance()
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .CreateLabel(out var label)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        CachedMethodInfo.m_FocusedOnVehicleMap.CallInstruction,
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        ((Delegate)AddVehicleRot).Method.CallInstruction)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsAngle))
      .Advance()
      .CreateLabel(out var label2)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Brfalse_S, label2),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        CachedMethodInfo.m_ExtraAngle.CallInstruction,
        new CodeInstruction(OpCodes.Add))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity))
      .Advance()
      .CreateLabel(out var label3)
      .Insert(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Brfalse_S, label3),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        CachedMethodInfo.m_ExtraAngle.CallInstruction,
        CachedMethodInfo.g_Vector3_up.CallInstruction,
        CachedMethodInfo.m_Quaternion_AngleAxis.CallInstruction,
        CachedMethodInfo.o_Quaternion_Multiply.CallInstruction)
      .InstructionEnumeration();
  }

  private static Rot4 AddVehicleRot(Rot4 rot, VehiclePawn vehicle) =>
    new(rot.AsInt + vehicle.FullRotation.RotForVehicleDraw().AsInt);
}

[HarmonyPatch(typeof(SpectatorCellFinder), nameof(SpectatorCellFinder.GraphicOffsetForRect))]
[PatchLevel(Level.Safe)]
public static class Patch_SpectatorCellFinder_GraphicOffsetForRect
{
  public static void Postfix(ref Vector3 __result) => __result = __result.ToBaseMapCoord();
}