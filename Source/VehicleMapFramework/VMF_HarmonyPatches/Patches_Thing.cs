﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Thing), nameof(Thing.Rotation), MethodType.Setter)]
[PatchLevel(Level.Sensitive)]
public static class Patch_Thing_Rotation
{
    public static void Prefix(Thing __instance, ref Rot4 value)
    {
        if (__instance is Pawn pawn && pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            if (pawn.pather?.Moving ?? false)
            {
                var angle = (pawn.pather.nextCell - pawn.Position).AngleFlat;
                value = Rot8.FromAngle(Ext_Math.RotateAngle(angle, vehicle.FullRotation.AsAngle));
            }
            //else if (!pawn.Drafted)
            //{
            //    value.AsInt += vehicle.Rotation.AsInt;
            //}
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
        var replaced = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseFullRotationDoor)))
            .MethodReplacer(CachedMethodInfo.g_Rot4_AsQuat, CachedMethodInfo.m_Rot8_AsQuatRef)
            .MethodReplacer(CachedMethodInfo.m_Rot4_Rotate, CachedMethodInfo.m_Rot8_Rotate);
        var codes = new CodeMatcher(replaced, generator);
        codes.MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Graphic), nameof(Graphic.MatAt))));
        codes.Advance(-1);
        codes.Insert(CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RotForVehicleDraw)));

        codes.Start();
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_Rot8_AsQuatRef));
        codes.Repeat(c =>
        {
            c.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
            c.CreateLabelWithOffsets(1, out var label);
            c.InsertAfterAndAdvance(
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Transform))),
                CodeInstruction.LoadField(typeof(SmashTools.Rendering.Transform), nameof(SmashTools.Rendering.Transform.rotation)),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.up))),
                CodeInstruction.Call(typeof(Quaternion), nameof(Quaternion.AngleAxis)),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply));
            ;
        });
        return codes.Instructions();
    }
}

[HarmonyPatch(typeof(Building_SupportedDoor), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_SupportedDoor_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
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
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf);
                yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                yield return new CodeInstruction(OpCodes.Ldloc_S, vehicle);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull2);
                yield return instruction.WithLabels(label);
            }
            else if (instruction.Calls(CachedMethodInfo.g_Thing_Rotation) && num < 2)
            {
                num++;
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseRotationVehicleDraw);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

//ソーラーパネルはGraphic_Singleで見た目上回転しないのでFullRotationがHorizontalだったら回転しない
[HarmonyPatch(typeof(CompPowerPlantSolar), nameof(CompPowerPlantSolar.PostDraw))]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompPowerPlantSolar_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
            .MethodReplacer(CachedMethodInfo.m_Rot4_Rotate, CachedMethodInfo.m_Rot8_Rotate).ToList();

        var label = generator.DefineLabel();
        var label2 = generator.DefineLabel();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_Rot8_Rotate)) - 1;
        codes[pos].labels.Add(label);
        codes[pos + 2].labels.Add(label2);
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.IsHorizontal))),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Pop),
            new CodeInstruction(OpCodes.Br_S, label2)
        ]);
        return codes;
    }
}

[HarmonyPatch(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.PostDraw))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPowerPlantWind_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
            .MethodReplacer(CachedMethodInfo.g_Rot4_FacingCell, CachedMethodInfo.g_Rot8_FacingCell)
            .MethodReplacer(CachedMethodInfo.g_Rot4_RighthandCell, CachedMethodInfo.m_Rot8Utility_RighthandCell)
            .MethodReplacer(CachedMethodInfo.m_Rot4_Rotate, CachedMethodInfo.m_Rot8_Rotate)
            .MethodReplacer(CachedMethodInfo.g_Rot4_AsQuat, CachedMethodInfo.m_Rot8_AsQuatRef)
            .MethodReplacer(CachedMethodInfo.m_IntVec3_ToVector3, CachedMethodInfo.m_Rot8Utility_ToFundVector3);
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
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing),
            CodeInstruction.Call(typeof(Patch_CompPowerPlantWind_RecalculateBlockages), nameof(Restrict))
        ]);

        return codes.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotation)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
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
        var ldcr4 = instructions.FirstOrDefault(c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(0.1f));
        ldcr4?.operand = 0.75f;
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
            .MethodReplacer(CachedMethodInfo.m_Rot4_Rotate, CachedMethodInfo.m_Rot8_Rotate);
    }
}

[HarmonyPatch(typeof(PlaceWorker_WindTurbine), nameof(PlaceWorker_WindTurbine.DrawGhost))]
[PatchLevel(Level.Safe)]
public static class Patch_PlaceWorker_WindTurbine_DrawGhost
{
    public static void Prefix(ref IntVec3 center, ref Rot4 rot, Thing thing)
    {
        if (Command_FocusVehicleMap.FocusedVehicle != null)
        {
            center = center.ToBaseMapCoord(Command_FocusVehicleMap.FocusedVehicle);
            rot.AsInt += Command_FocusVehicleMap.FocusedVehicle.Rotation.AsInt;
        }
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            center = center.ToBaseMapCoord(vehicle);
            rot.AsInt += vehicle.Rotation.AsInt;
        }
    }
}

[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.PostDraw))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompRefuelable_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
            .MethodReplacer(CachedMethodInfo.m_Rot4_Rotate, CachedMethodInfo.m_Rot8_Rotate);
    }
}

[HarmonyPatch(typeof(PlaceWorker_FuelingPort), nameof(PlaceWorker_FuelingPort.DrawFuelingPortCell))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_FuelingPort_DrawFuelingPortCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedDrawPosOffset)
        ]);
        return codes;
    }
}

//Vehicleは移動するからTickごとにTileを取得し直す
[HarmonyPatch(typeof(TravellingTransporters), "TickInterval")]
[PatchLevel(Level.Safe)]
public static class Patch_TravellingTransporters_Tick
{
    public static void Postfix(TravellingTransporters __instance)
    {
        if (__instance.arrivalAction is TransportersArrivalAction_LandInSpecificCell arrivalAction && mapParent(arrivalAction) is MapParent_Vehicle mapParent_Vehicle)
        {
            __instance.destinationTile = mapParent_Vehicle.Tile;
        }
    }

    private static AccessTools.FieldRef<TransportersArrivalAction_LandInSpecificCell, MapParent> mapParent
        = AccessTools.FieldRefAccess<TransportersArrivalAction_LandInSpecificCell, MapParent>("mapParent");
}

//ワイヤーの行き先オフセットとFillableBarの回転
[HarmonyPatch(typeof(Building_MechCharger), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_MechCharger_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var rot = generator.DeclareLocal(typeof(Rot4));
        var f_rotation = AccessTools.Field(typeof(GenDraw.FillableBarRequest), nameof(GenDraw.FillableBarRequest.rotation));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld && c.OperandIs(f_rotation));
        //一度Rot4に格納しないとエラー出したので
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Stloc_S, rot),
            new CodeInstruction(OpCodes.Ldloc_S, rot),
        ]);

        var pos2 = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_IntVec3_ToVector3Shifted)) + 1;
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var label = generator.DefineLabel();

        codes[pos2].labels.Add(label);
        codes.InsertRange(pos2,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2)
        ]);
        return codes.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing);
    }
}

//ThingがあればThing.Map、なければFocusedVehicle.VehicleMap、それもなければFind.CurrentMapを参照するようにする
[HarmonyPatch(typeof(PlaceWorker_WatchArea), nameof(PlaceWorker_WatchArea.DrawGhost))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_WatchArea_DrawGhost
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
        var label = generator.DefineLabel();
        var label2 = generator.DefineLabel();

        codes[pos].labels.Add(label2);
        codes.InsertRange(pos - 1,
        [
            CodeInstruction.LoadArgument(5),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Br_S, label2),
            new CodeInstruction(OpCodes.Pop).WithLabels(label)
        ]);
        pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenDraw_DrawFieldEdges));
        codes.Insert(pos, CodeInstruction.LoadLocal(0));
        return codes.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap)
            .MethodReplacer(CachedMethodInfo.m_GenDraw_DrawFieldEdges, CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges);
    }
}

//マップ外からPawnFlyerが飛んでくることが起こりうるので(MeleeAnimationのLassoなど)領域外の時はPositionのセットをスキップする
[HarmonyPatch(typeof(PawnFlyer), "RecomputePosition")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PawnFlyer_RecomputePosition
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var s_Position = AccessTools.PropertySetter(typeof(Thing), nameof(Thing.Position));
        var pos = codes.FindLastIndex(c => c.opcode == OpCodes.Call && c.OperandIs(s_Position));

        var label = generator.DefineLabel();
        var m_InBounds = AccessTools.Method(typeof(GenGrid), nameof(GenGrid.InBounds), [typeof(IntVec3), typeof(Map)]);

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

[HarmonyPatch(typeof(PawnFlyer), nameof(PawnFlyer.DestinationPos), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_PawnFlyer_DestinationPos
{
    public static void Postfix(PawnFlyer __instance, ref Vector3 __result)
    {
        if (__instance.Map.IsNonFocusedVehicleMapOf(out var vehicle))
        {
            __result = __result.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool))]
[PatchLevel(Level.Safe)]
public static class Patch_GenSpawn_Spawn
{
    public static void Prefix(Thing newThing, ref Map map, IntVec3 loc)
    {
        if (map == null)
        {
            return;
        }
        if (newThing is Projectile)
        {
            map = map.BaseMap();
        }
        else if (newThing is Mote && !loc.InBounds(map))
        {
            map = map.BaseMap();
        }
    }
}

[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanConstruct), typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool), typeof(JobDef))]
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
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        instructions = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Stloc_2 || instruction.opcode == OpCodes.Stloc_3 || (instruction.opcode == OpCodes.Stloc_S && ((LocalBuilder)instruction.operand).LocalIndex == 4))
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
                yield return CodeInstruction.Call(typeof(Vector3Utility), nameof(Vector3Utility.RotatedBy), [typeof(Vector3), typeof(float)]);
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

        ThingDef thingDef = GenConstruct.BuiltDefOf(thing.def) as ThingDef;
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