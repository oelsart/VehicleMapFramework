using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_PerspectiveShift
{
  static Patches_PerspectiveShift()
  {
    if (PerspectiveShift)
    {
      VMF_Harmony.PatchCategory(PatchCategories.PerspectiveShift);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.State", "OnGUI")]
[PatchLevel(Level.Sensitive)]
public static class Patch_State_OnGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    CodeInstruction[] code = [new(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map)];
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .Repeat(matcher => matcher.InsertAndAdvance(code).InsertAfter(code).Advance())
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "UpdateCamera")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Avatar_UpdateCamera
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    CodeInstruction[] code = [new(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map)];
    var matcher = new CodeMatcher(instructions, generator);
    matcher
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .Repeat(matcher2 => matcher2.InsertAndAdvance(code).InsertAfter(code).Advance())
      .Reset()
      .MatchEndForward(
        CodeMatch.Calls(AccessTools.Method(typeof(Vector3?), nameof(Nullable<>.GetValueOrDefault))),
        new CodeMatch(OpCodes.Stloc_2));
    var code2 = CodeInstruction.LoadArgument(0).MoveLabelsFrom(matcher.Instruction);
    return matcher
      .CreateLabel(out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .Insert(
        code2,
        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field("PerspectiveShift.Avatar:pawn")),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2))
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "UpdateInput")]
[PatchLevel(Level.Safe)]
public static class Patch_Avatar_UpdateInput
{
  public static void Postfix(ref Vector3 ___moveInput, Pawn ___pawn)
  {
    if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      ___moveInput = ___moveInput.RotatedBy(-vehicle.FullAngle);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "ProcessMovement")]
[PatchLevel(Level.Safe)]
public static class Patch_Avatar_ProcessMovement
{
  private static CompZipline compZipline;

  private static readonly AccessTools.FieldRef<PawnTweener, Vector3> tweenedPos =
    AccessTools.FieldRefAccess<PawnTweener, Vector3>("tweenedPos");

  public static bool Prefix(Vector3 ___moveInput, ref Vector3? ___physicsPosition, ref IntVec3 ___prevCell, Pawn ___pawn)
  {
    if (___pawn.Map is null || ___physicsPosition is null)
    {
      return true;
    }

    ___pawn.IsOnVehicleMapOf(out var vehicle);
    if (compZipline is not null)
    {
      if (compZipline.parent is { Spawned: false } ||
          ___pawn.Position != compZipline.parent.Position ||
          compZipline.Pair is not { Spawned: true })
      {
        compZipline = null;
        return true;
      }
      if (___moveInput != Vector3.zero)
      {
        var drawPosA = ___pawn.DrawPos;
        var drawPosB = compZipline.Pair.DrawPos;
        var drawPosC = compZipline.parent.DrawPos;
        var line = drawPosB - drawPosA;
        var pathLength = line.MagnitudeHorizontal();
        var totalLengthSquared = (drawPosC - drawPosB).MagnitudeHorizontalSquared();
        if (totalLengthSquared < pathLength * pathLength)
        {
          compZipline = null;
          return true;
        }

        var normalized = new Vector3(line.x / pathLength, 0f, line.z / pathLength);
        if (vehicle is not null)
        {
          normalized = normalized.RotatedBy(vehicle.FullAngle);
        }

        const float distancePerTick = 0.075f;
        //ジップラインの先端から登る場合は遅くなるわな
        var back = Vector3.Dot(normalized, ___moveInput) < 0f;
        var moveDistance = compZipline.IsZiplineEnd ^ back ? distancePerTick * 0.5f : distancePerTick;
        if (back) moveDistance *= -1f;

        ___physicsPosition += normalized * moveDistance;
        if ((drawPosC - drawPosA).MagnitudeHorizontalSquared() > totalLengthSquared)
        {
          RespawnPawn(___pawn, compZipline.Pair.Position, compZipline.Pair.Map, out ___prevCell);
          ___physicsPosition = ___physicsPosition.Value
            .ToThingBaseMapCoord(compZipline.parent)
            .ToNonFocusedThingMapCoord(compZipline.Pair);
          tweenedPos(___pawn.Drawer.tweener) = ___physicsPosition.Value;
          compZipline = null;
        }
      }
      return false;
    }
    var comp = ___pawn.Position.GetThingList(___pawn.Map).Select(t => t.TryGetComp<CompZipline>()).FirstOrDefault();
    if (comp is { Pair.Spawned: true })
    {
      if ((comp.Pair.DrawPos - ___pawn.DrawPos).MagnitudeHorizontalSquared() <
          (comp.Pair.DrawPos - comp.parent.DrawPos).MagnitudeHorizontalSquared())
      {
        compZipline = comp;
        return true;
      }
    }

    if (___moveInput == Vector3.zero)
    {
      if (!___physicsPosition.Value.ToIntVec3().WalkableBy(___pawn.Map, ___pawn))
      {
        ___pawn.pather.TryRecoverFromUnwalkablePosition(false);
        ___physicsPosition = ___pawn.Position.ToVector3Shifted();
        return false;
      }
      return true;
    }
    switch (vehicle)
    {
      case { Spawned: true } when vehicle.CachedWalkableMapEdgeCells.Keys.Contains(___pawn.Position):
      {
        var baseMapCoord = ___physicsPosition.Value.ToBaseMapCoord(vehicle);
        var baseMapMove = ___moveInput.RotatedBy(vehicle.FullAngle);
        var moveToTentative = baseMapCoord + baseMapMove / 2f;
        if (moveToTentative.TryGetVehicleMap(vehicle.Map, vehicle, VehicleMapFlag.None))
        {
          if (!___physicsPosition.Value.ToIntVec3().WalkableBy(___pawn.Map, ___pawn))
          {
            ___pawn.pather.TryRecoverFromUnwalkablePosition(false);
            ___physicsPosition = ___pawn.Position.ToVector3Shifted();
          }
          return true;
        }
        var groundMap = vehicle.Map;
        var ticks = ___pawn.TicksPerMoveCardinal * 4f;
        if (!___pawn.Position.GetThingList(vehicle.VehicleMap)
              .Any(t => t.TryGetComp<CompVehicleEnterSpot>(out var comp2) && comp2 is not CompZipline))
          ticks *= 2f;
        var offset = Time.deltaTime * (60f / ticks) * baseMapMove;

        var moveTo = baseMapCoord + offset;
        var moveToCell = moveTo.ToIntVec3();
        if (!moveToCell.InBounds(groundMap)) return true;

        var thingList = moveToCell.GetThingList(groundMap);
        var containsVehicle = false;
        for (var i = 0; i < thingList.Count; i++)
        {
          if (thingList[i] == vehicle)
          {
            containsVehicle = true;
            break;
          }
        }
        if (containsVehicle)
        {
          ___physicsPosition += Time.deltaTime * (60f / ticks) * ___moveInput;
          var rot = Rot8.FromAngle(___moveInput.AngleFlat());
          if (vehicle.Rotation.IsHorizontal) rot = rot.RotForVehicleDraw();
          ___pawn.Rotation = rot;
          if (___pawn.Position.TryGetFirstThing<Building_VehicleRamp>(vehicle.VehicleMap, out var ramp))
          {
            ramp.StartManualOpenBy(___pawn);
          }
          return false;
        }
        if (moveToCell.Walkable(groundMap))
        {
          RespawnPawn(___pawn, moveToCell, groundMap, out ___prevCell);
          ___physicsPosition = moveTo;
          tweenedPos(___pawn.Drawer.tweener) = ___physicsPosition.Value;
          return false;
        }
        break;
      }
      case null:
      {
        var moveTo = ___physicsPosition.Value + ___moveInput;
        var moveToCell = moveTo.ToIntVec3();
        if (moveToCell.InBounds(___pawn.Map) && moveToCell.TryGetFirstThing(___pawn.Map, out vehicle))
        {
          var vehicleMapCoord = moveTo.ToVehicleMapCoord(vehicle);
          var intVec3 = vehicleMapCoord.ToIntVec3();
          var edgeCell = moveToCell.ClosestEdgeCell(vehicle);
          if (!edgeCell.IsValid) return true;
          var ticks = ___pawn.TicksPerMoveCardinal * 4f;
          if (edgeCell.AdjacentTo8WayOrInside(intVec3))
          {
            if (edgeCell.GetDoor(vehicle.VehicleMap) is { } door && door.PawnCanOpen(___pawn))
            {
              door.StartManualOpenBy(___pawn);
            }
            if (edgeCell.TryGetFirstThing<Building_VehicleRamp>(vehicle.VehicleMap, out var ramp))
            {
              ramp.StartManualOpenBy(___pawn);
            }

            if (!edgeCell.GetThingList(vehicle.VehicleMap)
                  .Any(t => t.TryGetComp<CompVehicleEnterSpot>(out var comp2) && comp2 is not CompZipline))
              ticks *= 2f;
          }

          var offset = Time.deltaTime * (60f / ticks) * ___moveInput;
          ___physicsPosition += offset;
          ___pawn.Rotation = Rot8.FromAngle(___moveInput.AngleFlat());

          if (___physicsPosition.Value.TryGetVehicleMap(___pawn.Map, vehicle, VehicleMapFlag.None))
          {
            var vehicleMapCoord2 = ___physicsPosition.Value.ToVehicleMapCoord(vehicle);
            var cell = vehicleMapCoord2.ToIntVec3();
            if (cell.Walkable(vehicle.VehicleMap))
            {
              RespawnPawn(___pawn, cell, vehicle.VehicleMap, out ___prevCell);
              ___physicsPosition = vehicleMapCoord2;
              tweenedPos(___pawn.Drawer.tweener) = ___physicsPosition.Value;
              return false;
            }

            ___physicsPosition -= offset;
          }

          return false;
        }
        break;
      }
    }

    return true;

    static void RespawnPawn(Pawn pawn, IntVec3 cell, Map map, out IntVec3 prevCell)
    {
      pawn.DeSpawnWithoutJobClear();
      GenSpawn.Spawn(pawn, cell, map);
      prevCell = IntVec3.Invalid;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "RotateTowardsMouse")]
[PatchLevel(Level.Cautious)]
public static class Patch_Avatar_RotateTowardsMouse
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "HandleLeftClickInt")]
[PatchLevel(Level.Safe)]
public static class Patch_Avatar_HandleLeftClickInt
{
  public static void Prefix(Pawn ___pawn, ref (VirtualTeleporter?, Command_FocusVehicleMap.FocusVehicle?) __state)
  {
    if (!___pawn.Spawned) return;
    var mouseMapPosition = UI.MouseMapPosition();
    var map = mouseMapPosition.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None)
      ? vehicle.VehicleMap
      : Find.CurrentMap;
    if (___pawn.Map != map)
    {
      var pos = ___pawn.PositionOnBaseMap;
      if (vehicle is not null)
      {
        if (!mouseMapPosition.ToVehicleMapCoord(vehicle).InBounds(vehicle.VehicleMap))
        {
          return; // 外のポーンから車両マップ外のクリック時エラーが出るのを防止
        }
        pos = pos.ToVehicleMapCoord(vehicle);
      }
      ___pawn.DepartMap = map;
      __state.Item1 = new VirtualTeleporter(___pawn, map, pos);
    }

    if (vehicle is not null)
    {
      __state.Item2 = new Command_FocusVehicleMap.FocusVehicle(vehicle);
    }
  }

  public static void Finalizer(Pawn ___pawn, (VirtualTeleporter?, Command_FocusVehicleMap.FocusVehicle?) __state)
  {
    ___pawn.RemoveDepartMap();
    __state.Item1?.Dispose();
    __state.Item2?.Dispose();
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "TryHandleFloatMenu")]
[PatchLevel(Level.Safe)]
public static class Patch_Avatar_TryHandleFloatMenu
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_DepartMapOrPawnMap);
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "DrawReticle")]
[PatchLevel(Level.Cautious)]
public static class Patch_Avatar_DrawReticle
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "GetBestTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Avatar_GetBestTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GetThingList))
      .SetOperandAndAdvance(CachedMethodInfo.m_GetThingListAcrossMaps)
      .Insert(CodeInstruction.Call(typeof(Patch_Avatar_GetBestTarget), nameof(RemoveMapVehicles)))
      .InstructionEnumeration();
  }

  private static List<Thing> RemoveMapVehicles(List<Thing> list)
  {
    list.RemoveAll(t => t is VehiclePawnWithMap);
    return list;
  }
}

[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.Avatar", "HandleFiring")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Avatar_HandleFiring
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
      .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

/// <summary>
/// 車両マップとのキャッシュフレームタイミングの違いによりエラーがでることがあるため、アバターの非アクティブ化を確実にする
/// </summary>
[HarmonyPatchCategory(PatchCategories.PerspectiveShift)]
[HarmonyPatch("PerspectiveShift.State", "ClearAvatar")]
[PatchLevel(Level.Safe)]
public static class Patch_State_ClearAvatar
{
  public static void Postfix(ref int ____isAvatarCacheFrame)
  {
    ____isAvatarCacheFrame = -1;
  }
}
