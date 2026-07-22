using System;
using System.Collections.Generic;
using System.Linq;
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

    if (Command_FocusVehicleMap.FocusedVehicle is { } vehicle &&
        !VehicleSectionLayerManager.CacheMode && !VehiclePawnWithMapCache.CacheMode)
    {
      __result = __result.ToBaseMapCoord(vehicle).WithY(__result.y);
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

//thingがIsOnVehicleMapだった場合回転の初期値num4にベースvehicleのAngleを与え、posはRotatePointで回転
[HarmonyPatchCategory(LatePatchCore.Category)]
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
[HarmonyAfter("owlchemist.smartfarming", "Helixien.ReGrowthCore")]
[PatchLevel(Level.Sensitive)]
public static class Patch_SelectionDrawer_DrawSelectionBracketFor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
    var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
    var label = generator.DefineLabel();

    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadLocal(2),
      new CodeInstruction(OpCodes.Ldloca_S, vehicle),
      CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf.CallInstruction,
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      CachedMethodInfo.m_FullAngle.CallInstruction,
      new CodeInstruction(OpCodes.Conv_I4),
      new CodeInstruction(OpCodes.Add),
    ]);

    var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 18);
    var label2 = generator.DefineLabel();

    codes[pos2].labels.Add(label2);
    codes.InsertRange(pos2,
    [
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      new CodeInstruction(OpCodes.Brfalse_S, label2),
      CodeInstruction.LoadLocal(2),
      CachedMethodInfo.g_Thing_DrawPos.CallvirtInstruction,
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      CachedMethodInfo.m_FullAngle.CallInstruction,
      new CodeInstruction(OpCodes.Neg),
      CachedMethodInfo.m_RotatePoint.CallInstruction
    ]);

    var m_DrawFieldEdges = SmartFarming.Active
      ? AccessTools.Method(SmartFarming.MapComponent_SmartFarming, "DrawFieldEdges")
      : CachedMethodInfo.m_GenDraw_DrawFieldEdges1;
    var m_DrawFieldEdgesOnVehicle =
      SmartFarming.SmartFarmingActive ? ((Delegate)GenDrawOnVehicle.DrawFieldEdgesSF).Method :
      SmartFarming.ReGrowthActive ? ((Delegate)GenDrawOnVehicle.DrawFieldEdgesRG).Method :
      CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges1;
    var pos3 = codes.FindIndex(c => c.Calls(m_DrawFieldEdges));
    codes[pos3].operand = m_DrawFieldEdgesOnVehicle;
    codes.InsertRange(pos3,
    [
      CodeInstruction.LoadLocal(0),
      CachedMethodInfo.g_Zone_Map.CallvirtInstruction
    ]);
    var pos4 = codes.FindIndex(pos3 + 3, c => c.Calls(CachedMethodInfo.m_GenDraw_DrawFieldEdges1));
    codes[pos4].operand = CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges1;
    codes.InsertRange(pos4,
    [
      CodeInstruction.LoadLocal(1),
      AccessTools.PropertyGetter(typeof(Plan), nameof(Plan.Map)).CallvirtInstruction
    ]);
    return codes;
  }
}

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.DrawLinesBetweenTargets))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Pawn_JobTracker_DrawLinesBetweenTargets
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Position));
    codes.RemoveRange(pos, 4);
    codes.Insert(pos, AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos)).CallvirtInstruction);

    var g_CenterVector3 = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3));
    var m_CenterVector3VehicleOffset = ((Delegate)CenterVector3VehicleOffset).Method;
    foreach (var code in codes)
    {
      if (code.opcode == OpCodes.Call && code.OperandIs(g_CenterVector3))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CodeInstruction.LoadField(typeof(Pawn_JobTracker), "pawn");
        code.operand = m_CenterVector3VehicleOffset;
      }

      yield return code;
    }
  }

  public static Vector3 CenterVector3VehicleOffset(ref LocalTargetInfo targ, Pawn pawn)
  {
    if (targ.HasThing)
    {
      if (targ.Thing.Spawned)
      {
        return targ.Thing.DrawPos;
      }

      return targ.Thing.SpawnedOrAnyParentSpawned
        ? targ.Thing.SpawnedParentOrMe.DrawPos
        : targ.Thing.Position.ToVector3Shifted();
    }

    if (!targ.Cell.IsValid) return default;

    if (pawn.TryGetTargetMap(out var map) && pawn.stances.curStance is Stance_Busy)
    {
      return targ.Cell.ToVector3Shifted().ToBaseMapCoord(map);
    }

    if (pawn.CurJob?.globalTarget.Map is { } map2)
    {
      return targ.Cell.ToVector3Shifted().ToBaseMapCoord(map2);
    }

    if (pawn.CurJob?.GetCachedDriver(pawn) is JobDriverAcrossMaps driver)
    {
      var destMap = driver.DestMap;
      if (destMap.IsNonFocusedVehicleMapOf(out var vehicle))
      {
        return targ.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle);
      }
    }
    else if (pawn.IsOnNonFocusedVehicleMapOf(out var vehicle) && pawn.stances.curStance is not Stance_Busy
             {
               verb: Verb_Jump or Verb_CastAbilityJump
             })
    {
      return targ.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle);
    }

    return targ.Cell.ToVector3Shifted();
  }
}

[HarmonyPatch(typeof(PawnPath), nameof(PawnPath.DrawPath))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PawnPath_DrawPath
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out var vehicle,
        getInstance: [CodeInstruction.LoadArgument(1)])
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
public static class Patch_Designation_DrawLoc
{
  [PatchLevel(Level.Safe)]
  public static void Postfix(ref Vector3 __result, DesignationManager ___designationManager, LocalTargetInfo ___target)
  {
    if (___designationManager.map.IsVehicleMapOf(out var vehicle))
    {
      if (!___target.HasThing)
      {
        __result = __result.ToBaseMapCoord(vehicle).WithY(__result.y);
      }
    }
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing),
      (CachedMethodInfo.g_Rot4_AsVector2, CachedMethodInfo.m_AsFundVector2));
  }
}

[HarmonyPatch(typeof(OverlayDrawer), "RenderPulsingOverlay", typeof(Thing), typeof(Material), typeof(int), typeof(Mesh),
  typeof(bool))]
public static class Patch_OverlayDrawer_RenderPulsingOverlay
{
  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing),
      (CachedMethodInfo.g_Rot4_AsVector2, CachedMethodInfo.m_AsFundVector2));
  }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawRadiusRing), typeof(IntVec3), typeof(float), typeof(Color),
  typeof(Func<IntVec3, bool>))]
[PatchLevel(Level.Safe)]
public static class Patch_GenDraw_DrawRadiusRing
{
  private static readonly List<IntVec3> ringDrawCells = [];

  public static bool Prefix(ref IntVec3 center, float radius, Color color, Func<IntVec3, bool> predicate)
  {
    Thing thing = null;
    var flag = false;
    foreach (var selObj in Find.Selector.SelectedObjects)
    {
      if (selObj is Thing thing2 && thing2.Position == center)
      {
        flag = true;
        thing = thing2;
        break;
      }
    }

    if (flag)
    {
      if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
      {
        if (Find.CurrentMap.IsNonFocusedVehicleMap &&
            Find.CurrentMap.BaseMapOrCaravan == vehicle.VehicleMap.BaseMapOrCaravan)
        {
          DrawRadiusRing(vehicle.VehicleMap, center, radius, color, predicate);
          return false;
        }

        center = center.ToBaseMapCoord(vehicle);
      }
    }
    else if (Command_FocusVehicleMap.FocusedVehicle != null)
    {
      center = center.ToBaseMapCoord(Command_FocusVehicleMap.FocusedVehicle);
    }

    return true;
  }

  private static void DrawRadiusRing(Map map, IntVec3 center, float radius, Color color,
    Func<IntVec3, bool> predicate = null)
  {
    if (radius > GenRadial.MaxRadialPatternRadius)
    {
      Log.ErrorOnce($"Cannot draw radius ring of radius {radius}: not enough squares in the precalculated list.",
        71496514);
      return;
    }

    ringDrawCells.Clear();
    var num = GenRadial.NumCellsInRadius(radius);
    for (var i = 0; i < num; i++)
    {
      var intVec = center + GenRadial.RadialPattern[i];
      if (predicate == null || predicate(intVec))
      {
        ringDrawCells.Add(intVec);
      }
    }

    GenDrawOnVehicle.DrawFieldEdges(ringDrawCells, color, map: map);
  }
}

//tDef.interactionCellGraphic.DrawFromDef(vector, rot, tDef.interactionCellIcon, 0f) ->
//tDef.interactionCellGraphic.DrawFromDef(vector, rot, tDef.interactionCellIcon, 0f)
//Graphics.DrawMesh(MeshPool.plane10, SelectedDrawPosOffset(vector, center), Quaternion.identity, GenDraw.InteractionCellMaterial, 0) ->
//Graphics.DrawMesh(MeshPool.plane10, FocusedDrawPosOffset(vector, center), Quaternion.identity, GenDraw.InteractionCellMaterial, 0)
[HarmonyPatch(typeof(GenDraw), "DrawInteractionCell")]
[PatchLevel(Level.Sensitive)]
public static class Patch_GenDraw_DrawInteractionCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 4);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(2),
      CachedMethodInfo.m_SelectedDrawPosOffset.CallInstruction
    ]);

    var pos2 = codes.FindIndex(pos,
      c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Quaternion_identity));
    codes.InsertRange(pos2,
    [
      CodeInstruction.LoadArgument(2),
      CachedMethodInfo.m_FocusedOrSelectedDrawPosOffset.CallInstruction
    ]);
    return codes;
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

[HarmonyPatch(typeof(PlaceWorker_ShowTradeBeaconRadius), nameof(PlaceWorker_ShowTradeBeaconRadius.DrawGhost))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_ShowTradeBeaconRadius_DrawGhost
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenDraw_DrawFieldEdges1));
    var label = generator.DefineLabel();
    codes[pos].operand = CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges1;
    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      new CodeInstruction(OpCodes.Ldnull),
      CodeInstruction.LoadArgument(5),
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Pop),
      CodeInstruction.LoadArgument(5),
      new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
    ]);
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