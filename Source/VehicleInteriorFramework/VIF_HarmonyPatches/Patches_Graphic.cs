using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
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
            __result.AsInt = (__result.AsInt + VehicleMapUtility.rotForPrint.AsInt) % 4;
        }
    }

    [HarmonyPatch(typeof(Pawn_RotationTracker), "FaceAdjacentCell")]
    public static class Patch_Pawn_RotationTracker_FaceAdjacentCell
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (___pawn.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                var rot = ___pawn.Rotation;
                rot.AsInt = (rot.AsInt + parentVehicle.vehicle.Rotation.AsInt) % 4;
                ___pawn.Rotation = rot;
            }
        }
    }

    [HarmonyPatch(typeof(CameraDriver), "ApplyPositionToGameObject")]
    public static class Patch_CameraDriver_ApplyPositionToGameObject
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 15f);
            codes[pos].operand = 20f;
            var pos2 = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 50f);
            codes[pos2].operand = 45f;
            return codes;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
    public static class Patch_PawnRenderer_GetBodyPos
    {
        public static void Postfix(PawnPosture posture, Pawn ___pawn, ref Vector3 __result)
        {
            var corpse = ___pawn.Corpse;
            if (corpse != null && corpse.Map != null && corpse.Map.Parent is MapParent_Vehicle parentVehicle1)
            {
                __result.y += parentVehicle1.vehicle.DrawPos.y + 0.09615385f;
            }
            else if (posture != PawnPosture.Standing && ___pawn.Map != null && ___pawn.Map.Parent is MapParent_Vehicle parentVehicle2)
            {
                __result.y += parentVehicle2.vehicle.DrawPos.y + 0.09615385f;
            }
        }
    }

    [HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawAimPie))]
    public static class Patch_GenDraw_DrawAimPie
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap)
                .MethodReplacer(VehicleMapUtility.m_TargetInfo_Cell, VehicleMapUtility.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
    public static class Patch_Pawn_TryStartAttack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_TargetInfo_Cell, VehicleMapUtility.m_CellOnBaseMap);
        }
    }
}
