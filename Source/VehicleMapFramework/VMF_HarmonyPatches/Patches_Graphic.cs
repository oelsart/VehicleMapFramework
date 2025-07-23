using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

//Graphic_Linked系統のリンクは、先にcを回転させておく。base.ShouldLinkWithを使っているところはスタブしておいたオリジナルのメソッドを使用
[HarmonyPatch(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith))]
public static class Patch_Graphic_Linked_ShouldLinkWith
{
    [PatchLevel(Level.Mandatory)]
    [HarmonyReversePatch(HarmonyReversePatchType.Original)]
    [HarmonyPriority(Priority.Normal)]
    //なんでReversePatchしてるのにオリジナルのメソッドをコピーしてるのかって？Performance AnalyzerがReversePatchに対応してないからだよ！
    public static bool ShouldLinkWith(Graphic_Linked instance, IntVec3 c, Thing parent)
    {
        if (!parent.Spawned)
        {
            return false;
        }
        if (!c.InBounds(parent.Map))
        {
            return (parent.def.graphicData.linkFlags & LinkFlags.MapEdge) > LinkFlags.None;
        }
        return (parent.Map.linkGrid.LinkFlagsAt(c) & parent.def.graphicData.linkFlags) > LinkFlags.None;
    }

    [PatchLevel(Level.Safe)]
    [HarmonyPriority(Priority.Low)]
    public static void Prefix(ref IntVec3 c, Thing parent)
    {
        if (VehicleMapUtility.RotForPrint != Rot4.North)
        {
            var offset = c - parent.Position;
            var rotated = offset.RotatedBy(VehicleMapUtility.RotForPrint.IsHorizontal ? VehicleMapUtility.RotForPrint.Opposite : VehicleMapUtility.RotForPrint);
            c = rotated + parent.Position;
        }
    }
}

[HarmonyPatch(typeof(Graphic_LinkedAsymmetric), nameof(Graphic_LinkedAsymmetric.ShouldLinkWith))]
public static class Patch_Graphic_LinkedAsymmetric_ShouldLinkWith
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(CachedMethodInfo.m_ShouldLinkWith, CachedMethodInfo.m_ShouldLinkWithOrig);

    [PatchLevel(Level.Safe)]
    public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
}

[HarmonyPatch(typeof(Graphic_LinkedTransmitter), nameof(Graphic_LinkedTransmitter.ShouldLinkWith))]
public static class Patch_Graphic_LinkedTransmitter_ShouldLinkWith
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(CachedMethodInfo.m_ShouldLinkWith, CachedMethodInfo.m_ShouldLinkWithOrig);

    [PatchLevel(Level.Safe)]
    public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
}

[HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
public static class Patch_Thing_Print
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

        codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
        codes.Insert(pos, CodeInstruction.LoadArgument(0));
        return codes;
    }
}

[HarmonyPatch(typeof(MinifiedThing), nameof(MinifiedThing.Print))]
public static class Patch_MinifiedThing_Print
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        var m_PrintPlane = AccessTools.Method(typeof(Printer_Plane), nameof(Printer_Plane.PrintPlane));
        codes.MatchStartForward(CodeMatch.Calls(m_PrintPlane));
        codes.MatchStartBackwards(new CodeMatch(c => c.opcode == OpCodes.Ldloc_1));
        codes.InsertAfterAndAdvance(CodeInstruction.LoadArgument(0));
        codes.Advance(1);
        codes.SetInstruction(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));

        codes.End();
        codes.MatchStartBackwards(CodeMatch.Calls(m_PrintPlane));
        codes.MatchStartBackwards(new CodeMatch(c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalType == typeof(Material)));
        codes.InsertAfterAndAdvance(CodeInstruction.LoadArgument(0));
        codes.Advance(1);
        codes.SetInstruction(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
        return codes.Instructions();
    }
}

[HarmonyPatch(typeof(Graphic), nameof(Graphic.Print))]
public static class Patch_Graphic_Print
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_3) - 1;

        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotateForPrintNegate),
            CodeInstruction.LoadArgument(2),
            CodeInstruction.Call(typeof(Patch_Graphic_Print), nameof(EdgeSpacerOffset)),
        ]);

        return codes.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint);
    }

    //はしごとかのマップ端オフセットを足す
    private static Vector3 EdgeSpacerOffset(Vector3 vector, Thing thing)
    {
        VehicleMapProps mapProps;
        if (thing.HasComp<CompVehicleEnterSpot>() && thing.IsOnVehicleMapOf(out var vehicle) && (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
        {
            var opposite = thing.Rotation.Opposite;
            return vector + (opposite.AsVector2.ToVector3() * mapProps.EdgeSpaceValue(VehicleMapUtility.RotForPrint, opposite));
        }
        return vector;
    }
}

//コーナーフィラーの位置の回転を打ち消す
//マップ端のフィラー位置調整機能も切る　この機能何？
[HarmonyPatch(typeof(Graphic_LinkedCornerFiller), nameof(Graphic_LinkedCornerFiller.Print))]
public static class Patch_Graphic_LinkedCornerFiller_Print
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var f_Altitudes_AltIncVect = AccessTools.Field(typeof(Altitudes), nameof(Altitudes.AltIncVect));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_Altitudes_AltIncVect)) - 1;

        codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotateForPrintNegate));

        var c_Vector3 = AccessTools.Constructor(typeof(Vector3), [typeof(float), typeof(float), typeof(float)]);
        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Newobj && c.OperandIs(c_Vector3)) + 1;
        codes.Insert(pos2, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotateForPrintNegate));

        var pos3 = codes.FindIndex(pos2, c => c.opcode == OpCodes.Brtrue);
        var label = codes[pos3].operand;
        var l_vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

        codes.InsertRange(pos3 + 1,
        [
            CodeInstruction.LoadArgument(2),
            new CodeInstruction(OpCodes.Ldloca, l_vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnVehicleMapOf),
            new CodeInstruction(OpCodes.Brtrue, label)
        ]);

        return codes;
    }
}

//Graphic_LinkedCornerOverlaySingleを使うためのWrap。linkDrawerTypeは適当に被らなそうな数字にしました。
[HarmonyPatchCategory(EarlyPatchCore.Category)]
[HarmonyPatch(typeof(GraphicUtility), nameof(GraphicUtility.WrapLinked))]
public static class Patch_GraphicUtility_WrapLinked
{
    [PatchLevel(Level.Mandatory)]
    public static bool Prefix(Graphic subGraphic, LinkDrawerType linkDrawerType, ref Graphic_Linked __result)
    {
        if ((byte)linkDrawerType == 56)
        {
            __result = new Graphic_LinkedCornerOverlaySingle(subGraphic);
            return false;
        }
        return true;
    }
}

//バニラのCopyFromがcornerOverlayPathをコピーしてないためにエラーがでてたので修正。
[HarmonyPatchCategory(EarlyPatchCore.Category)]
[HarmonyPatch(typeof(GraphicData), nameof(GraphicData.CopyFrom))]
public static class Patch_GraphicData_CopyFrom
{
    [PatchLevel(Level.Mandatory)]
    public static void Postfix(GraphicData __instance, GraphicData other)
    {
        __instance.cornerOverlayPath = other.cornerOverlayPath;
    }
}

//カメラの制限範囲を書き換える。CurrentMapがVehicleMapだったらDrawSizeの長辺を参照する
[HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Update))]
public static class Patch_CameraDriver_Update
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var g_Thing_DrawSize = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawSize));
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var isVehicleMap = generator.DeclareLocal(typeof(bool));
        var longSide = generator.DeclareLocal(typeof(float));
        var drawSize = generator.DeclareLocal(typeof(Vector2));

        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(2f)) + 1;
        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(-2f));
        var label = generator.DefineLabel();
        var label2 = generator.DefineLabel();
        codes[pos].labels.Add(label);
        codes[pos2].labels.Add(label2);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadField(typeof(VehicleMapFramework), nameof(VehicleMapFramework.settings)),
            CodeInstruction.LoadField(typeof(VehicleMapSettings), nameof(VehicleMapSettings.drawPlanet)),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Find_CurrentMap),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsVehicleMapOf),
            new CodeInstruction(OpCodes.Stloc_S, isVehicleMap),
            new CodeInstruction(OpCodes.Ldloc_S, isVehicleMap),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Callvirt, g_Thing_DrawSize),
            new CodeInstruction(OpCodes.Stloc_S, drawSize),
            new CodeInstruction(OpCodes.Ldloc_S, drawSize),
            CodeInstruction.LoadField(typeof(Vector2), nameof(Vector2.x)),
            new CodeInstruction(OpCodes.Ldloc_S, drawSize),
            CodeInstruction.LoadField(typeof(Vector2), nameof(Vector2.y)),
            CodeInstruction.Call(typeof(Mathf), nameof(Mathf.Max), [typeof(float), typeof(float)]),
            new CodeInstruction(OpCodes.Stloc_S, longSide),
            new CodeInstruction(OpCodes.Ldloc_S, longSide),
            new CodeInstruction(OpCodes.Br_S, label2),
        ]);

        pos = codes.FindIndex(pos2, c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(2f)) + 1;
        pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(-2f));
        var label3 = generator.DefineLabel();
        var label4 = generator.DefineLabel();
        codes[pos].labels.Add(label3);
        codes[pos2].labels.Add(label4);
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Ldloc_S, isVehicleMap),
            new CodeInstruction(OpCodes.Brfalse_S, label3),
            new CodeInstruction(OpCodes.Ldloc_S, longSide),
            new CodeInstruction(OpCodes.Br_S, label4),
        ]);

        return codes;
    }
}

[HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
public static class Patch_PawnRenderer_GetBodyPos
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(PawnPosture posture, Pawn ___pawn, ref Vector3 __result)
    {
        var corpse = ___pawn.Corpse;
        if (corpse != null && corpse.IsOnNonFocusedVehicleMapOf(out _))
        {
            corpse.TryGetDrawPos(ref __result);
        }
        else if (___pawn.ParentHolder is VehicleRoleHandlerBuildable)
        {
            __result = __result.YOffsetFull();
        }
        else if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            if (___pawn.CurrentBed() != null)
            {
                __result = __result.ToBaseMapCoord(vehicle).WithYOffset(-0.9615385f / VehicleMapUtility.YCompress);
            }
            else if (posture != PawnPosture.Standing)
            {
                __result = __result.YOffsetFull(vehicle);
            }
        }
    }
}

[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BodyAngle))]
public static class Patch_PawnRenderer_BodyAngle
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(Pawn ___pawn, ref float __result)
    {
        if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            __result = Ext_Math.RotateAngle(__result, vehicle.FullRotation.AsAngle);
        }
    }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawAimPie))]
public static class Patch_GenDraw_DrawAimPie
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(Thing shooter, ref LocalTargetInfo target)
    {
        if (!target.HasThing && TargetMapManager.HasTargetMap(shooter, out var map))
        {
            target = target.Cell.ToBaseMapCoord(map);
        }
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ProcessPostTickVisuals))]
public static class Patch_Pawn_ProcessPostTickVisuals
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.ExactPosition), MethodType.Getter)]
public static class Patch_Projectile_ExactPosition
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(ref Vector3 __result)
    {
        __result = __result.YOffsetFull();
    }
}

[HarmonyPatch(typeof(Graphic_Shadow), nameof(Graphic_Shadow.DrawWorker))]
public static class Patch_Graphic_Shadow_DrawWorker
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var f_MatBases_SunShadowFade = AccessTools.Field(typeof(MatBases), nameof(MatBases.SunShadowFade));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_MatBases_SunShadowFade));
        var label = generator.DefineLabel();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

        codes[pos].labels.Add(label);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(4),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_FullRotation),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_Rot8_AsQuat),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply)
        ]);
        return codes.MethodReplacer(CachedMethodInfo.g_Rot4_AsQuat, CachedMethodInfo.m_Rot8_AsQuatRef);
    }
}

[HarmonyPatch(typeof(Frame), "DrawAt")]
public static class Patch_Frame_DrawAt
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.OperandIs(CachedMethodInfo.m_Matrix4x4_SetTRS))
            {
                yield return CodeInstruction.LoadArgument(0);
                instruction.operand = CachedMethodInfo.m_SetTRSOnVehicle;
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawFillableBar))]
public static class Patch_GenDraw_DrawFillableBar
{
    [PatchLevel(Level.Safe)]
    public static bool Prefix(GenDraw.FillableBarRequest r)
    {
        if (r.rotation.AsInt >= 4)
        {
            var rot = new Rot8(r.rotation.AsInt);
            Vector2 vector = r.preRotationOffset.RotatedBy(rot.AsAngle);
            r.center += new Vector3(vector.x, 0f, vector.y);
            if (rot == Rot8.NorthEast)
            {
                rot = Rot8.SouthWest;
            }
            if (rot == Rot8.SouthEast)
            {
                rot = Rot8.NorthWest;
            }
            Vector3 s = new(r.size.x + r.margin, 1f, r.size.y + r.margin);
            Matrix4x4 matrix = default;
            matrix.SetTRS(r.center, rot.AsQuat(), s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, r.unfilledMat, 0);
            if (r.fillPercent > 0.001f)
            {
                s = new Vector3(r.size.x * r.fillPercent, 1f, r.size.y);
                matrix = default;
                Vector3 pos = r.center + (Vector3.up * 0.01f);
                pos += new Vector3((-r.size.x * 0.5f) + (0.5f * r.size.x * r.fillPercent), 0f, 0f).RotatedBy(rot.AsAngle);
                matrix.SetTRS(pos, rot.AsQuat(), s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, r.filledMat, 0);
            }
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(MapDrawer), "ViewRect", MethodType.Getter)]
public static class Patch_MapDrawer_ViewRect
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap, CachedMethodInfo.m_ClipInsideVehicleMap);
    }
}

[HarmonyPatch(typeof(GenView), nameof(GenView.ShouldSpawnMotesAt))]
public static class Patch_GenView_ShouldSpawnMotesAt
{
    [PatchLevel(Level.Safe)]
    [HarmonyPatch([typeof(Vector3), typeof(Map), typeof(bool)])]
    public static void Prefix(ref Map map)
    {
        offset = false;
        if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            offset = true;
            map = vehicle.Map;
        }
    }

    [PatchLevel(Level.Safe)]
    [HarmonyPatch([typeof(IntVec3), typeof(Map), typeof(bool)])]
    public static void Prefix(ref IntVec3 loc, ref Map map)
    {
        if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            loc = loc.ToBaseMapCoord(vehicle);
            map = vehicle.Map;
        }
    }

    public static bool offset;
}

[HarmonyPatch]
public static class Patch_SubEffecter_Sprayer
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(SubEffecter_Sprayer), nameof(SubEffecter_Sprayer.GetAttachedSpawnLoc));
        yield return AccessTools.Method(typeof(SubEffecter_Sprayer), "MakeMote");
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var g_CenterVector3 = AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.CenterVector3));
        var m_CenterVector3ToBaseMap = AccessTools.Method(typeof(Patch_SubEffecter_Sprayer), nameof(CenterVector3ToBaseMap));
        return instructions.MethodReplacer(g_CenterVector3, m_CenterVector3ToBaseMap);
    }

    private static Vector3 CenterVector3ToBaseMap(ref TargetInfo targetInfo)
    {
        var result = targetInfo.CenterVector3;
        if (!targetInfo.HasThing && targetInfo.Map.IsNonFocusedVehicleMapOf(out var vehicle))
        {
            result = result.ToBaseMapCoord(vehicle);
        }
        return result;
    }
}