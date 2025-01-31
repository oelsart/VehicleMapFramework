using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    //Graphic_Linked系統のリンクは、先にcを回転させておく。base.ShouldLinkWithを使っているところはスタブしておいたオリジナルのメソッドを使用
    [HarmonyPatch(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith))]
    public static class Patch_Graphic_Linked_ShouldLinkWith
    {
        [HarmonyReversePatch(HarmonyReversePatchType.Original)]
        public static bool ShouldLinkWith(Graphic_Linked instance, IntVec3 c, Thing parent) => throw new NotImplementedException();

        public static void Prefix(ref IntVec3 c, Thing parent)
        {
            var offset = c - parent.Position;
            var rotated = offset.RotatedBy(VehicleMapUtility.rotForPrint.IsHorizontal ? VehicleMapUtility.rotForPrint.Opposite : VehicleMapUtility.rotForPrint);
            c = rotated + parent.Position;
        }
    }

    [HarmonyPatch(typeof(Graphic_LinkedAsymmetric), nameof(Graphic_LinkedAsymmetric.ShouldLinkWith))]
    public static class Patch_Graphic_LinkedAsymmetric_ShouldLinkWith
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(MethodInfoCache.m_ShouldLinkWith, MethodInfoCache.m_ShouldLinkWithOrig);

        public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
    }

    [HarmonyPatch(typeof(Graphic_LinkedTransmitter), nameof(Graphic_LinkedTransmitter.ShouldLinkWith))]
    public static class Patch_Graphic_LinkedTransmitter_ShouldLinkWith
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(MethodInfoCache.m_ShouldLinkWith, MethodInfoCache.m_ShouldLinkWithOrig);

        public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
    public static class Patch_Thing_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

            codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PrintExtraRotation));
            codes.Insert(pos, CodeInstruction.LoadArgument(0));
            return codes;
        }
    }

    [HarmonyPatch(typeof(MinifiedThing), nameof(MinifiedThing.Print))]
    public static class Patch_MinifiedThing_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var m_PrintPlane = AccessTools.Method(typeof(Printer_Plane), nameof(Printer_Plane.PrintPlane));

            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_PrintPlane));
            var pos2 = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldloc_1) + 1;
            codes.Replace(codes[pos2], new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PrintExtraRotation));
            codes.Insert(pos2, CodeInstruction.LoadArgument(0));

            pos = codes.FindIndex(pos + 1, c => c.opcode == OpCodes.Call && c.OperandIs(m_PrintPlane));
            pos2 = codes.FindLastIndex(c => c.opcode == OpCodes.Ldloc_S && (c.operand as LocalBuilder).LocalIndex == 4) + 1;
            codes.Replace(codes[pos2], new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PrintExtraRotation));
            codes.Insert(pos2, CodeInstruction.LoadArgument(0));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Rotation), MethodType.Getter)]
    public static class Patch_Thing_Rotation
    {
        [HarmonyReversePatch(HarmonyReversePatchType.Original)] 
        public static Rot4 Rotation(Thing instance) => throw new NotImplementedException();

        [HarmonyPatch(MethodType.Setter)]
        public static void Prefix(Thing __instance, ref Rot4 value)
        {
            if (__instance is Pawn pawn && (pawn.pather?.Moving ?? false) && pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                var angle = (pawn.pather.nextCell - pawn.Position).AngleFlat;
                if (angle != 0f)
                {
                    value = Rot8.FromAngle(Ext_Math.RotateAngle(angle, vehicle.FullRotation.AsAngle));
                }
                else
                {
                    value.AsInt += vehicle.FullRotation.AsInt;
                }
            }
        }

        [HarmonyPatch(MethodType.Getter)]
        public static void Postfix(ref Rot4 __result, Thing __instance)
        {
            if (VehicleMapUtility.rotForPrint != Rot4.North && (__instance.def.size.x != __instance.def.size.z || __instance.def.rotatable || (__instance.def.graphicData?.drawRotated ?? false) && __instance.Graphic is Graphic_Multi))
            {
                __result.AsInt += VehicleMapUtility.rotForPrint.AsInt;
            }
        }
    }

    [HarmonyPatch(typeof(Graphic), nameof(Graphic.Print))]
    public static class Patch_Graphic_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_3) - 1;

            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_RotateForPrintNegate),
                CodeInstruction.LoadArgument(2),
                CodeInstruction.Call(typeof(Patch_Graphic_Print), nameof(Patch_Graphic_Print.EdgeSpacerOffset)),
            });
            return codes;
        }

        //はしごとかのマップ端オフセットを足す
        private static Vector3 EdgeSpacerOffset(Vector3 vector, Thing thing)
        {
            VehicleMapProps mapProps;
            if (thing.HasComp<CompVehicleEnterSpot>() && thing.IsOnVehicleMapOf(out var vehicle) && (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
            {
                var opposite = Patch_Thing_Rotation.Rotation(thing).Opposite;
                return vector + opposite.AsVector2.ToVector3() * mapProps.EdgeSpaceValue(VehicleMapUtility.rotForPrint, opposite);
            }
            return vector;
        }
    }

    //CompPrintForPowerGridからこれを呼んだ時rotForPrintによってRotationがずれてnullを返しちゃってたので修正
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.GetWallAttachedTo), typeof(Thing))]
    public static class Patch_GenConstruct_GetWallAttachedTo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_Thing_RotationOrig);
        }
    }

    //オフセットの修正
    [HarmonyPatch(typeof(PowerNetGraphics), nameof(PowerNetGraphics.PrintWirePieceConnecting))]
    public static class Patch_PowerNetGraphics_PrintWirePieceConnecting
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_Thing_RotationOrig);
        }
    }

    [HarmonyPatch(typeof(Graphic_Shadow), nameof(Graphic_Shadow.Print))]
    public static class Patch_Graphic_Shadow_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_Thing_RotationOrig);
        }
    }

    //レイヤー全体にオフセットをかけるのでこの中のDrawPosはオフセット無し版に変更
    //コーナーフィラーの位置の回転を打ち消す
    //マップ端のフィラー位置調整機能も切る　この機能何？
    [HarmonyPatch(typeof(Graphic_LinkedCornerFiller), nameof(Graphic_LinkedCornerFiller.Print))]
    public static class Patch_Graphic_LinkedCornerFiller_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var f_Altitudes_AltIncVect = AccessTools.Field(typeof(Altitudes), nameof(Altitudes.AltIncVect));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_Altitudes_AltIncVect)) - 1;

            codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_RotateForPrintNegate));

            var c_Vector3 = AccessTools.Constructor(typeof(Vector3), new Type[] { typeof(float), typeof(float), typeof(float) });
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Newobj && c.OperandIs(c_Vector3)) + 1;
            codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_RotateForPrintNegate));

            var pos3 = codes.FindIndex(pos2, c => c.opcode == OpCodes.Brtrue);
            var label = codes[pos3].operand;
            var l_vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes.InsertRange(pos3 + 1, new[]
            {
                CodeInstruction.LoadArgument(2),
                new CodeInstruction(OpCodes.Ldloca, l_vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brtrue, label)
            });

            return codes;
        }
    }

    //Graphic_LinkedCornerOverlaySingleを使うためのWrap。linkDrawerTypeは適当に被らなそうな数字にしました。
    [HarmonyPatch(typeof(GraphicUtility), nameof(GraphicUtility.WrapLinked))]
    [HarmonyPatchCategory("VehicleInteriors.EarlyPatches")]
    public static class Patch_GraphicUtility_WrapLinked
    {
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
    [HarmonyPatch(typeof(GraphicData), nameof(GraphicData.CopyFrom))]
    [HarmonyPatchCategory("VehicleInteriors.EarlyPatches")]
    public static class Patch_GraphicData_CopyFrom
    {
        public static void Postfix(GraphicData __instance, GraphicData other)
        {
            __instance.cornerOverlayPath = other.cornerOverlayPath;
        }
    }

    //[HarmonyPatch(typeof(Pawn_RotationTracker), "FaceAdjacentCell")]
    //public static class Patch_Pawn_RotationTracker_FaceAdjacentCell
    //{
    //    public static void Prefix(ref IntVec3 c, Pawn ___pawn)
    //    {
    //        if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
    //        {
    //            c = ___pawn.Position + (c - ___pawn.Position).RotatedBy(vehicle.Rotation);
    //        }
    //    }
    //}

    //ズームしすぎて車上オブジェクトがカメラの手前に来ないようにする
    [HarmonyPatch(typeof(CameraDriver), "ApplyPositionToGameObject")]
    public static class Patch_CameraDriver_ApplyPositionToGameObject
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 15f);
            codes[pos].operand = 25f;
            var pos2 = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 50f);
            codes[pos2].operand = 40f;
            return codes;
        }
    }

    //カメラの制限範囲を書き換える。CurrentMapがVehicleMapだったらDrawSizeの長辺を参照する
    [HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Update))]
    public static class Patch_CameraDriver_Update
    {
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
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadField(typeof(VehicleInteriors), nameof(VehicleInteriors.settings)),
                CodeInstruction.LoadField(typeof(VehicleMapSettings), nameof(VehicleMapSettings.drawPlanet)),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Find_CurrentMap),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsVehicleMapOf),
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
                CodeInstruction.Call(typeof(Mathf), nameof(Mathf.Max), new Type[] { typeof(float), typeof(float) }),
                new CodeInstruction(OpCodes.Stloc_S, longSide),
                new CodeInstruction(OpCodes.Ldloc_S, longSide),
                new CodeInstruction(OpCodes.Br_S, label2),
            });

            pos = codes.FindIndex(pos2, c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(2f)) + 1;
            pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldc_R4 && c.OperandIs(-2f));
            var label3 = generator.DefineLabel();
            var label4 = generator.DefineLabel();
            codes[pos].labels.Add(label3);
            codes[pos2].labels.Add(label4);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, isVehicleMap),
                new CodeInstruction(OpCodes.Brfalse_S, label3),
                new CodeInstruction(OpCodes.Ldloc_S, longSide),
                new CodeInstruction(OpCodes.Br_S, label4),
            });

            return codes;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
    public static class Patch_PawnRenderer_GetBodyPos
    {
        public static void Postfix(PawnPosture posture, Pawn ___pawn, ref Vector3 __result)
        {
            var corpse = ___pawn.Corpse;
            if (corpse != null && corpse.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                corpse.TryGetOnVehicleDrawPos(ref __result);
            }
            else if (___pawn.ParentHolder is VehicleHandlerBuildable)
            {
                __result.y += VehicleMapUtility.altitudeOffsetFull;
            }
            else if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle2))
            {
                if (___pawn.CurrentBed() != null)
                {
                    __result = __result.ToBaseMapCoord(vehicle2).WithYOffset(-0.9615385f);
                }
                else if (posture != PawnPosture.Standing)
                {
                    __result.y += vehicle2.DrawPos.y;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BodyAngle))]
    public static class Patch_PawnRenderer_BodyAngle
    {
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
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ProcessPostTickVisuals))]
    public static class Patch_Pawn_ProcessPostTickVisuals
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.ExactPosition), MethodType.Getter)]
    public static class Patch_Projectile_ExactPosition
    {
        public static void Postfix(ref Vector3 __result)
        {
            __result = __result.WithYOffset(VehicleMapUtility.altitudeOffsetFull);
        }
    }

    [HarmonyPatch(typeof(Graphic_Shadow), nameof(Graphic_Shadow.DrawWorker))]
    public static class Patch_Graphic_Shadow_DrawWorker
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var f_MatBases_SunShadowFade = AccessTools.Field(typeof(MatBases), nameof(MatBases.SunShadowFade));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_MatBases_SunShadowFade));
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(4),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_Rot8_AsQuat),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.o_Quaternion_Multiply)
            });
            return codes.MethodReplacer(MethodInfoCache.g_Rot4_AsQuat, MethodInfoCache.m_Rot8_AsQuatRef);
        }
    }

    [HarmonyPatch(typeof(Frame), "DrawAt")]
    public static class Patch_Frame_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.OperandIs(MethodInfoCache.m_Matrix4x4_SetTRS))
                {
                    yield return CodeInstruction.LoadArgument(0);
                    instruction.operand = MethodInfoCache.m_SetTRSOnVehicle;
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawFillableBar))]
    public static class Patch_GenDraw_DrawFillableBar
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Rot4_AsAngle, MethodInfoCache.g_Rot8_AsAngle)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsQuat, MethodInfoCache.m_Rot8_AsQuatRef);
        }
    }

    [HarmonyPatch(typeof(MapDrawer), "ViewRect", MethodType.Getter)]
    public static class Patch_MapDrawer_ViewRect
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_CellRect_ClipInsideMap, MethodInfoCache.m_ClipInsideVehicleMap);
        }
    }
}