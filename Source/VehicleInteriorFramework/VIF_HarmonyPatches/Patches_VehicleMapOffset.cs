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

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(UI), nameof(UI.MouseCell))]
    public static class Patch_UI_MouseCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var toIntVec3 = AccessTools.Method(typeof(IntVec3Utility), nameof(IntVec3Utility.ToIntVec3));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(toIntVec3));
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_VehicleMapToOrig1));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawPos), MethodType.Getter)]
    public static class Patch_Thing_DrawPos
    {
        public static bool Prefix(Thing __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    public static class Patch_Pawn_DrawPos
    {
        public static bool Prefix(Pawn __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DrawPos), MethodType.Getter)]
    public static class Patch_VehiclePawn_DrawPos
    {
        public static bool Prefix(VehiclePawn __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }

        public static void Postfix(VehiclePawn __instance, ref Vector3 __result)
        {
            __result += __instance.jobs?.curDriver?.ForcedBodyOffset ?? Vector3.zero;
        }
    }

    [HarmonyPatch(typeof(AttachableThing), nameof(AttachableThing.DrawPos), MethodType.Getter)]
    public static class Patch_AttachableThing_DrawPos
    {
        public static bool Prefix(AttachableThing __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(MechShield), nameof(MechShield.DrawPos), MethodType.Getter)]
    public static class Patch_MechShield_DrawPos
    {
        public static bool Prefix(MechShield __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(Mote), nameof(Mote.DrawPos), MethodType.Getter)]
    public static class Patch_Mote_DrawPos
    {
        public static bool Prefix(Mote __instance, ref Vector3 __result)
        {
            return !__instance.TryGetOnVehicleDrawPos(ref __result);
        }
    }

    [HarmonyPatch(typeof(FleckStatic), nameof(FleckStatic.DrawPos), MethodType.Getter)]
    public static class Patch_FleckStatic_DrawPos
    {
        public static void Postfix(Map ___map, ref Vector3 __result)
        {
            if (___map?.Parent is MapParent_Vehicle parentVehicle)
            {
                __result = __result.OrigToVehicleMap(parentVehicle.vehicle);
            }
        }
    }

    //描画位置をOrigToVehicleMapで調整して回転はextraRotationに渡す
    [HarmonyPatch(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing))]
    public static class Patch_GhostDrawer_DrawGhostThing
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var getTrueCenter = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), new Type[] { typeof(IntVec3), typeof(Rot4), typeof(IntVec2), typeof(float) });
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getTrueCenter)) + 1;
            codes.Insert(pos, CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3) }));

            var label = generator.DefineLabel();
            var drawFromDef = AccessTools.Method(typeof(Graphic), nameof(Graphic.DrawFromDef));
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(drawFromDef));
            var rot = generator.DeclareLocal(typeof(Rot8));
            codes[pos2].labels.Add(label);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }

    [HarmonyDebug]
    //thingがIsOnVehicleMapだった場合回転の初期値num3にベースvehicleのAngleを与え、posはRotatePointで回転
    [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
    public static class Patch_SelectionDrawer_DrawSelectionBracketFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 8);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithInterior));
            var rot = generator.DeclareLocal(typeof(Rot8));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Add),
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 16);
            var label2 = generator.DefineLabel();
            var g_DrawPos = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos));

            codes[pos2].labels.Add(label2);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Callvirt, g_DrawPos),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Neg),
                CodeInstruction.Call(typeof(Ext_Math), nameof(Ext_Math.RotatePoint))
            });
            return codes;
        }
    }

    [HarmonyPatch("Vehicles.Rendering", "DrawSelectionBracketsVehicles")]
    public static class Patch_Rendering_DrawSelectionBracketsVehicles
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindLastIndex(c => c.opcode == OpCodes.Stloc_3);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithInterior));
            var rot = generator.DeclareLocal(typeof(Rot8));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }
}
