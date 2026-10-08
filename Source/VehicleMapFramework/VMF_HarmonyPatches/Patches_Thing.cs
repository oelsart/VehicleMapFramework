using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Thing), nameof(Thing.Rotation), MethodType.Setter)]
[PatchLevel(Level.Sensitive)]
public static class Patch_Thing_Rotation
{
  public static void Prefix(Thing __instance, ref Rot4 value)
  {
    if (__instance is Pawn pawn and not VehiclePawn && pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      if (pawn.pather is { Moving: true, nextCell.IsValid: true } && pawn.pather.nextCell != pawn.Position)
      {
        var angle = (pawn.pather.nextCell - pawn.Position).AngleFlat;
        value = Rot8.FromAngle(Ext_Math.RotateAngle(angle, vehicle.FullAngle));
      }
      else if (pawn.stances.curStance is Stance_Busy stance)
      {
        if (!stance.focusTarg.HasThing)
          value = Pawn_RotationTracker.RotFromAngleBiased(
            (stance.focusTarg.TargetCellOnBaseMap(pawn) - pawn.PositionOnBaseMap).AngleFlat);
      }
      else if (!pawn.Drafted && !pawn.HostileTo(Faction.OfPlayer))
      {
        value.AsInt += vehicle.Rotation.AsInt;
      }
    }
  }
}

[HarmonyPatch(typeof(Building_Door), "StuckOpen", MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Building_Door_StuckOpen
{
  public static void Postfix(Building_Door __instance, ref bool __result)
  {
    __result &= __instance is not Building_VehicleRamp;
  }
}

[HarmonyPatch(typeof(Building_Door), "DrawMovers")]
public static class Patch_Building_Door_DrawMovers
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(ref float altitude, Building_Door __instance)
  {
    if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      altitude = altitude.YOffsetFull(vehicle);
    }
  }

  [PatchLevel(Level.Sensitive)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var asQuatMatch = CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsQuat);
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(asQuatMatch).Advance()
      .NonFocusedMapVehicleForThing(out var vehicle)
      .MultiplyExtraAngleQuat(vehicle)
      .MatchStartForward(asQuatMatch).Advance()
      .MultiplyExtraAngleQuat(vehicle)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationForDoor);
  }
}

[HarmonyPatch(typeof(Building_SupportedDoor), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_SupportedDoor_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var f_Vector3_y = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));

    var num = 0;
    foreach (var instruction in instructions)
    {
      if (instruction.StoresField(f_Vector3_y))
      {
        var label = generator.DefineLabel();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        yield return CodeInstruction.LoadArgument(0);
        yield return new CodeInstruction(OpCodes.Ldloca_S, vehicle);
        yield return CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf.CallInstruction;
        yield return new CodeInstruction(OpCodes.Brfalse_S, label);
        yield return new CodeInstruction(OpCodes.Ldloc_S, vehicle);
        yield return CachedMethodInfo.m_YOffsetFull.CallInstruction;
        yield return instruction.WithLabels(label);
      }
      else if (instruction.Calls(CachedMethodInfo.g_Thing_Rotation) && num < 2)
      {
        num++;
        yield return CachedMethodInfo.m_BaseRotationVehicleDraw.CallInstruction;
      }
      else
      {
        yield return instruction;
      }
    }
  }
}

[HarmonyPatch(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.PostDraw))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPowerPlantWind_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var callsMultiply = CodeMatch.Calls(
      AccessTools.Method(typeof(Vector3), "op_Multiply", [typeof(Vector3), typeof(float)]));
    var callsAsQuat = CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsQuat);
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .NonFocusedMapVehicleForThingComp(out var vehicle)
      .MatchStartForward(callsMultiply).Advance()
      .RotatedByVehicleExtraAngle(vehicle)
      .MatchStartForward(callsMultiply).Advance()
      .RotatedByVehicleExtraAngle(vehicle)
      .MatchStartForward(callsAsQuat).Advance()
      .MultiplyExtraAngleQuat(vehicle)
      .MatchStartForward(callsAsQuat).Advance()
      .MultiplyExtraAngleQuat(vehicle)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.CompTick))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPowerPlantWind_CompTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(CompPowerPlantWind), "RecalculateBlockages")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompPowerPlantWind_RecalculateBlockages
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadField(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.parent)),
      CachedMethodInfo.m_BaseMap_Thing.CallInstruction,
      ((Delegate)Restrict).Method.CallInstruction
    ]);

    return codes.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }

  private static IEnumerable<IntVec3> Restrict(IEnumerable<IntVec3> enumerable, Map map)
  {
    return enumerable.Where(c => c.InBounds(map));
  }
}

[HarmonyPatch(typeof(Building_Battery), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_Battery_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.PostDraw))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompRefuelable_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(PlaceWorker_FuelingPort), nameof(PlaceWorker_FuelingPort.DrawFuelingPortCell))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_FuelingPort_DrawFuelingPortCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3ShiftedWithAltitude2))
      .InsertAfter(CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction)
      .InstructionEnumeration();
  }
}

//Vehicleは移動するからTickごとにTileを取得し直す
[HarmonyPatch(typeof(TravellingTransporters), "TickInterval")]
[PatchLevel(Level.Safe)]
public static class Patch_TravellingTransporters_Tick
{
  public static void Postfix(TravellingTransporters __instance)
  {
    if (__instance.arrivalAction is TransportersArrivalAction_LandInSpecificCell arrivalAction &&
        mapParent(arrivalAction) is MapParent_Vehicle mapParent_Vehicle)
    {
      __instance.destinationTile = mapParent_Vehicle.Tile;
    }
  }

  private static readonly AccessTools.FieldRef<TransportersArrivalAction_LandInSpecificCell, MapParent> mapParent
    = AccessTools.FieldRefAccess<TransportersArrivalAction_LandInSpecificCell, MapParent>("mapParent");
}

//ワイヤーの行き先オフセットとFillableBarの回転
[HarmonyPatch(typeof(Building_MechCharger), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_MechCharger_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
      .InsertAfter(CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(Building_MechCharger), nameof(Building_MechCharger.BarDrawData), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_MechCharger_BarDrawData
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(Building_MechGestator), "Tick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_MechGestator_Tick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatch(typeof(Building_MechGestator), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_MechGestator_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .NonFocusedMapVehicleForThing(out var vehicle)
      .AddAltitudeFor(vehicle) // AltitudeLayer.BuildingBelowTop.AltitudeFor()
      .AddAltitudeFor(vehicle, 0.01f) // AltitudeLayer.BuildingOnTop.AltitudeFor()
      .Reset()
      // Mathf.PingPong(...) => Mathf.PingPong(...).RotatedBy(vehicle.ExtraAngle)
      .MatchStartForward(CodeMatch.Calls(((Delegate)Mathf.PingPong).Method)).Advance()
      .AddExtraAngle(vehicle)
      // 0f => vehicle.ExtraAngle
      .MatchEndForward(
        CodeMatch.LoadsConstant(0f), CodeMatch.Calls(AccessTools.Method(typeof(Graphic), nameof(Graphic.Draw))))
      .Repeat(c => c.AddExtraAngle(vehicle))
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

//マップ外からPawnFlyerが飛んでくることが起こりうるので(MeleeAnimationのLassoなど)領域外の時はPositionのセットをスキップする
[HarmonyPatch(typeof(PawnFlyer), "RecomputePosition")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PawnFlyer_RecomputePosition
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var s_Position = AccessTools.PropertySetter(typeof(Thing), nameof(Thing.Position));
    var pos = codes.FindLastIndex(c => c.opcode == OpCodes.Call && c.OperandIs(s_Position));

    var label = generator.DefineLabel();
    var m_InBounds = ((Func<IntVec3, Map, bool>)GenGrid.InBounds).Method;

    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      new CodeInstruction(OpCodes.Dup),
      CodeInstruction.LoadArgument(0),
      new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
      new CodeInstruction(OpCodes.Call, m_InBounds),
      new CodeInstruction(OpCodes.Brtrue_S, label),
      new CodeInstruction(OpCodes.Pop),
      new CodeInstruction(OpCodes.Pop),
      new CodeInstruction(OpCodes.Ret)
    ]);
    return codes;
  }
}

[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4),
  typeof(WipeMode), typeof(bool), typeof(bool))]
[PatchLevel(Level.Safe)]
public static class Patch_GenSpawn_Spawn
{
  public static void Prefix(Thing newThing, ref Map map, IntVec3 loc)
  {
    if (map == null)
    {
      return;
    }

    switch (newThing)
    {
      case Projectile and not Spark:
      case Mote when !loc.InBounds(map):
        map = map.BaseMap();
        break;
      case PawnFlyer flyer when flyer.FlyingPawn.TryGetTargetMap(out var map2):
        map = map2;
        flyer.FlyingPawn.RemoveTargetInfo();
        break;
    }
  }
}

[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanConstruct), typeof(Thing), typeof(Pawn), typeof(bool),
  typeof(bool), typeof(JobDef))]
[PatchLevel(Level.Sensitive)]
public static class Patch_GenConstruct_CanConstruct
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
    codes[pos - 1].opcode = OpCodes.Ldarg_0;
    codes[pos].operand = CachedMethodInfo.g_Thing_Map;
    return codes;
  }
}

[HarmonyPatch(typeof(Building_Bookcase), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_Bookcase_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    instructions =
      instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
    foreach (var instruction in instructions)
    {
      if (instruction.opcode == OpCodes.Stloc_2 || instruction.opcode == OpCodes.Stloc_3 ||
          (instruction.opcode == OpCodes.Stloc_S && ((LocalBuilder)instruction.operand).LocalIndex == 4))
      {
        var label = generator.DefineLabel();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

        yield return CodeInstruction.LoadArgument(0);
        yield return new CodeInstruction(OpCodes.Ldloca_S, vehicle);
        yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf);
        yield return new CodeInstruction(OpCodes.Brfalse_S, label);
        yield return new CodeInstruction(OpCodes.Ldloc_S, vehicle);
        yield return new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Angle);
        yield return new CodeInstruction(OpCodes.Neg);
        yield return CachedMethodInfo.m_RotatedBy.CallInstruction;
        yield return instruction.WithLabels(label);
      }
      else
      {
        yield return instruction;
      }
    }
  }
}

[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.GetWallAttachedTo), typeof(Thing))]
[PatchLevel(Level.Mandatory)]
public static class Patch_GenConstruct_GetWallAttachedTo
{
  public static void Postfix(Thing thing, ref Thing __result)
  {
    if (__result is not null) return;
    if (thing.def.PlaceWorkers.All(p => p is not PlaceWorker_AttachedWallMultiCell)) return;

    var thingDef = GenConstruct.BuiltDefOf(thing.def) as ThingDef;
    if (thingDef?.building == null || !thingDef.building.isAttachment)
    {
      return;
    }

    var rot = thing.Rotation;
    var occupiedRect = thing.OccupiedRect();
    __result = GenConstruct.GetWallAttachedTo(occupiedRect.GetCenterCellOnEdge(rot), rot, thing.Map);
    if (__result is not null) return;
    if (occupiedRect.GetSideLength(thing.Rotation) % 2 == 1) return;
    __result = GenConstruct.GetWallAttachedTo(occupiedRect.GetCenterCellOnEdge(rot, -1), rot, thing.Map);
  }
}

[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.FindPreferredInteractionCell))]
[PatchLevel(Level.Mandatory)]
public static class Patch_Building_Bed_FindPreferredInteractionCell
{
  public static void Prefix(Building_Bed __instance, ref CellSearchPattern customSearchPattern)
  {
    if (__instance is Building_Hatch && customSearchPattern is null)
    {
      customSearchPattern = Building_Hatch.customBedInteractionCellsOrder;
    }
  }
}

// pawnとtravellerのマップが違う可能性を考慮しpawn.Map -> traveller.MapHeld
[HarmonyPatch(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.FindPodFor))]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompBiosculpterPod_FindPodFor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(new CodeMatch(OpCodes.Ldarg_0), CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .SetOpcodeAndAdvance(OpCodes.Ldarg_1)
      .Set(OpCodes.Call, CachedMethodInfo.g_Thing_MapHeld)
      .InstructionEnumeration();
  }
}

// 多くのPsilocapが高頻度でAllPawnsSpawnedを呼ぶ可能性がある
[HarmonyPatch(typeof(Plant_Psilocap), "TickInterval")]
[PatchLevel(Level.Cautious)]
public static class Patch_Plant_Psilocap_TickInterval
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_AllPawnsSpawned, CachedMethodInfo.m_AllPawnsSpawned_Reverse);
  }
}