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

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith))]
    public static class Patch_Graphic_Linked_ShouldLinkWith
    {
        public static void Prefix(ref IntVec3 c, Thing parent)
        {
            var offset = c - parent.Position;
            var rotated = offset.RotatedBy(VehicleMapUtility.rotForPrint.IsHorizontal ? VehicleMapUtility.rotForPrint.Opposite : VehicleMapUtility.rotForPrint);
            c = rotated + parent.Position;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
    public static class Patch_Thing_Print
    {
        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

            codes.Replace(codes[pos], CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PrintExtraRotation)));
            codes.Insert(pos, CodeInstruction.LoadArgument(0));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Rotation), MethodType.Getter)]
    public static class Patch_Thing_Rotation
    {
        public static void Postfix(ref Rot4 __result)
        {
            __result.AsInt += VehicleMapUtility.rotForPrint.AsInt;
        }

        [HarmonyReversePatch]
        public static Rot4 Rotation(Thing instance) => throw new NotImplementedException();
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
                CodeInstruction.LoadField(typeof(VehicleMapUtility), nameof(VehicleMapUtility.rotForPrint), true),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rot4), nameof(Rot4.AsAngle))),
                new CodeInstruction(OpCodes.Neg),
                CodeInstruction.Call(typeof(Vector3Utility), nameof(Vector3Utility.RotatedBy), new Type[]{ typeof(Vector3), typeof(float) }),
            });
            return codes;
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
            var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_DrawPos, MethodInfoCache.m_DrawPosOrig).ToList();
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

    [HarmonyPatch(typeof(Pawn_RotationTracker), "FaceAdjacentCell")]
    public static class Patch_Pawn_RotationTracker_FaceAdjacentCell
    {
        public static void Postfix(Pawn ___pawn)
        {
            ___pawn.Rotation = ___pawn.BaseRotationOfThing();
        }
    }

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

    [HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
    public static class Patch_PawnRenderer_GetBodyPos
    {
        public static void Postfix(Vector3 drawLoc, PawnPosture posture, Pawn ___pawn, ref Vector3 __result)
        {
            var corpse = ___pawn.Corpse;
            if (corpse != null && corpse.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                __result.y += vehicle.cachedDrawPos.y;
            }
            else if (___pawn.IsOnVehicleMapOf(out var vehicle2) && vehicle2.Spawned)
            {
                if (___pawn.CurrentBed() != null)
                {
                    __result = __result.OrigToVehicleMap(vehicle2).WithYOffset(-1.923077f);
                }
                else if (posture != PawnPosture.Standing)
                {
                    __result.y += vehicle2.cachedDrawPos.y;
                }
            }
            else if (___pawn.SpawnedParentOrMe is VehiclePawnWithMap)
            {
                __result.y = drawLoc.y;
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

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.DrawPos), MethodType.Getter)]
    public static class Patch_Projectile_DrawPos
    {
        public static void Postfix(ref Vector3 __result)
        {
            __result = __result.WithYOffset(VehicleMapUtility.altitudeOffsetFull);
        }
    }

    [HarmonyPatch(typeof(DesignationDragger), nameof(DesignationDragger.DraggerUpdate))]
    public static class Patch_DesignationDragger_DraggerUpdate
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var m_CellRect_ClipInsideRect = AccessTools.Method(typeof(CellRect), nameof(CellRect.ClipInsideRect));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_CellRect_ClipInsideRect));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[] {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[]{ typeof(CellRect), typeof(VehiclePawnWithMap) }))
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation), MethodType.Getter)]
    public static class Patch_VehiclePawn_FullRotation
    {
        public static void Postfix(VehiclePawn __instance, ref Rot8 __result)
        {
            if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                __result = new Rot8(new Rot4(__instance.Rotation.AsInt + vehicle.Rotation.AsInt), (__instance.Angle + vehicle.Angle) % 90f);
            }
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
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Spawned),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_Rot8_AsQuat),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.o_Quaternion_Multiply)
            });
            return codes;
        }
    }
}
