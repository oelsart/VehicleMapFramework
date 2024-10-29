using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_Designator_ZoneAdd_MakeNewZone
    {
        static Patches_Designator_ZoneAdd_MakeNewZone()
        {
            var transpiler = AccessTools.Method(typeof(Patches_Designator_ZoneAdd_MakeNewZone), nameof(Patches_Designator_ZoneAdd_MakeNewZone.Transpiler));
            foreach (var type in typeof(Designator_ZoneAdd).AllSubclasses())
            {
                var method = AccessTools.Method(type, "MakeNewZone");
                if (method != null && method.IsDeclaredMember())
                {
                    VIF_Harmony.Instance.Patch(method, null, null, transpiler);
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Find_CurrentMap, MethodInfoCache.g_VehicleMapUtility_CurrentMap);
        }
    }

    [StaticConstructorOnStartup]
    public static class Patches_Designator_Cells_SelectedUpdate
    {
        static Patches_Designator_Cells_SelectedUpdate()
        {
            var postfix = AccessTools.Method(typeof(Patch_Designator_Build_SelectedUpdate), nameof(Patch_Designator_Build_SelectedUpdate.Postfix));
            foreach (var type in typeof(Designator_Cells).AllSubclasses())
            {
                var method = AccessTools.Method(type, "SelectedUpdate");
                if (method != null && method.IsDeclaredMember())
                {
                    VIF_Harmony.Instance.Patch(method, null, postfix);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Designator), nameof(Designator.Map), MethodType.Getter)]
    public static class Patch_Designator_Map
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Find_CurrentMap, MethodInfoCache.g_VehicleMapUtility_CurrentMap);
        }
    }

    [HarmonyPatch(typeof(Designator), nameof(Designator.Deselected))]
    public static class Patch_Designator_Deselected
    {
        public static void Postfix()
        {
            if (Command_FocusVehicleMap.FocuseLockedVehicle == null)
            {
                Command_FocusVehicleMap.FocusedVehicle = null;
            }
        }
    }

    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.SelectedUpdate))]
    public static class Patch_Designator_Build_SelectedUpdate
    {
        public static void Postfix()
        {
            if (Command_FocusVehicleMap.FocuseLockedVehicle != null) return;

            Command_FocusVehicleMap.FocusedVehicle = null;
            var mousePos = UI.MouseMapPosition();
            var vehicles = VehiclePawnWithMapCache.allVehicles[Find.CurrentMap];
            var vehicle = vehicles.FirstOrDefault(v =>
            {
                var rect = new Rect(0f, 0f, (float)v.interiorMap.Size.x, (float)v.interiorMap.Size.z);
                var vector = mousePos.VehicleMapToOrig(v);

                return rect.Contains(new Vector2(vector.x, vector.z));
            });
            Command_FocusVehicleMap.FocusedVehicle = vehicle;
        }
    }

    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.Deselected))]
    public static class Patch_Designator_Build_Deselected
    {
        public static void Postfix()
        {
            Patch_Designator_Deselected.Postfix();
        }
    }

    //drawPosを移動してQuaternionに車の回転をかける。ほぼ同じなので3つまとめました
    [HarmonyPatch(typeof(GenUI), nameof(GenUI.RenderMouseoverBracket))]
    public static class Patch_GenUI_RenderMouseoverBracket
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var f_GenUIMouseoverBracketMaterial = AccessTools.Field(typeof(GenUI), "MouseoverBracketMaterial");
            return Patch_GenUI_RenderMouseoverBracket.TranspilerCommon(instructions, generator, f_GenUIMouseoverBracketMaterial);
        }

        public static IEnumerable<CodeInstruction> TranspilerCommon(IEnumerable<CodeInstruction> instructions, ILGenerator generator, FieldInfo field)
        {
            var codes = instructions.ToList();
            var m_QuaternionIdentity = AccessTools.PropertyGetter(typeof(Quaternion), nameof(Quaternion.identity));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_QuaternionIdentity));
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap1),
                new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.altitudeOffsetFull),
                new CodeInstruction(OpCodes.Neg),
                CodeInstruction.Call(typeof(Vector3Utility), nameof(Vector3Utility.WithYOffset))
            });

            var label = generator.DefineLabel();
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldsfld && c.OperandIs(field));
            codes[pos2].labels.Add(label);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FocusedVehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_Rot8_AsQuat),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.o_Quaternion_Multiply),
            });
            return codes;
        }
    }


    [HarmonyPatch(typeof(DesignatorUtility), nameof(DesignatorUtility.RenderHighlightOverSelectableCells))]
    public static class Patch_DesignatorUtility_RenderHighlightOverSelectableCells
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var f_DesignatorUtility_DragHighlightCellMat = AccessTools.Field(typeof(DesignatorUtility), nameof(DesignatorUtility.DragHighlightCellMat));
            return Patch_GenUI_RenderMouseoverBracket.TranspilerCommon(instructions, generator, f_DesignatorUtility_DragHighlightCellMat);
        }
    }

    [HarmonyPatch(typeof(DesignatorUtility), nameof(DesignatorUtility.RenderHighlightOverSelectableThings))]
    public static class Patch_DesignatorUtility_RenderHighlightOverSelectableThings
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var f_DesignatorUtility_DragHighlightThingMat = AccessTools.Field(typeof(DesignatorUtility), nameof(DesignatorUtility.DragHighlightThingMat));
            return Patch_GenUI_RenderMouseoverBracket.TranspilerCommon(instructions, generator, f_DesignatorUtility_DragHighlightThingMat);
        }
    }

    [HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.DrawDesignations))]
    public static class Patch_DesignationManager_DrawDesignations
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.InNoZoneEdgeArea))]
    public static class Patch_GenGrid_InNoZoneEdgeArea
    {
        public static void Postfix(ref bool __result, Map map)
        {
            __result = __result && !map.IsVehicleMapOf(out _);
        }
    }

    [HarmonyPatch(typeof(Designator_Zone), nameof(Designator_Zone.SelectedUpdate))]
    public static class Patch_Designator_Zone_SelectedUpdate
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.m_GenDraw_DrawFieldEdges));
            codes[pos].operand = MethodInfoCache.m_GenDrawOnVehicle_DrawFieldEdges;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Designator_Map),
            });
            return codes;
        }
    }
}
