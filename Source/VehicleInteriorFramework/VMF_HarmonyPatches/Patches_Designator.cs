using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
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
                    VMF_Harmony.Instance.Patch(method, null, null, transpiler);
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Find_CurrentMap, MethodInfoCache.g_VehicleMapUtility_CurrentMap);
        }
    }

    [StaticConstructorOnStartup]
    public static class Patches_Designator_SelectedUpdate
    {
        static Patches_Designator_SelectedUpdate()
        {
            var postfix = AccessTools.Method(typeof(Patches_Designator_SelectedUpdate), nameof(Patches_Designator_SelectedUpdate.Postfix));
            foreach (var type in typeof(Designator).AllSubclasses().Concat(typeof(Designator)).Except(typeof(Designator_AreaAllowed)))
            {
                var method = AccessTools.Method(type, "SelectedUpdate");
                if (method != null && method.IsDeclaredMember())
                {
                    VMF_Harmony.Instance.Patch(method, null, postfix);
                }
            }
        }

        public static void Postfix()
        {
            if (Command_FocusVehicleMap.FocuseLockedVehicle != null) return;

            Command_FocusVehicleMap.FocusedVehicle = null;
            var mousePos = UI.MouseMapPosition();
            if (mousePos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class Patches_Designator_DesignateThing
    {
        static Patches_Designator_DesignateThing()
        {
            var transpiler = AccessTools.Method(typeof(Patches_Designator_DesignateThing), nameof(Patches_Designator_DesignateThing.Transpiler));
            foreach (var type in typeof(Designator).AllSubclasses())
            {
                var method = AccessTools.Method(type, "DesignateThing");
                if (method != null && method.IsDeclaredMember())
                {
                    VMF_Harmony.Instance.Patch(method, null, null, transpiler);
                }
                var method2 = AccessTools.Method(type, "CanDesignateThing");
                if (method2 != null && method2.IsDeclaredMember())
                {
                    VMF_Harmony.Instance.Patch(method2, null, null, transpiler);
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(MethodInfoCache.g_Designator_Map))
                {
                    var label = generator.DefineLabel();
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return CodeInstruction.LoadArgument(1);
                    yield return new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_MapHeld);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Brtrue_S, label);
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return CodeInstruction.LoadArgument(0);
                    yield return new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Designator_Map);
                    yield return new CodeInstruction(OpCodes.Nop).WithLabels(label);
                }
                else
                {
                    yield return instruction;
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
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Quaternion_identity));
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_ToBaseMapCoord1),
                new CodeInstruction(OpCodes.Ldc_R4, AltitudeLayer.MetaOverlays.AltitudeFor()),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_Vector3Utility_WithY)
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

    [HarmonyPatch(typeof(Designator_Cancel), nameof(Designator_Cancel.RenderHighlight))]
    public static class Patch_Designator_Cancel_RenderHighlight
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var f_DesignatorUtility_DragHighlightCellMat = AccessTools.Field(typeof(DesignatorUtility), nameof(DesignatorUtility.DragHighlightCellMat));
            return Patch_GenUI_RenderMouseoverBracket.TranspilerCommon(instructions, generator, f_DesignatorUtility_DragHighlightCellMat);
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

    //利用可能なthingに車上マップ上のthingを含める
    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.ProcessInput))]
    public static class Patch_Designator_Build_ProcessInput
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var g_Count = AccessTools.PropertyGetter(typeof(List<Thing>), nameof(List<Thing>.Count));
            var pos = code.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(g_Count));
            code.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Designator_Map),
                CodeInstruction.LoadLocal(4),
                CodeInstruction.Call(typeof(Patch_ItemAvailability_ThingsAvailableAnywhere), nameof(Patch_ItemAvailability_ThingsAvailableAnywhere.AddThingList))
            });
            return code;
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
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToVehicleMapCoord), new Type[]{ typeof(CellRect) }))
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(Area), nameof(Area.MarkForDraw))]
    public static class Patch_Area_MarkForDraw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Find_CurrentMap, MethodInfoCache.g_VehicleMapUtility_CurrentMap);
        }
    }

    [HarmonyPatch(typeof(CellBoolDrawer), "ActuallyDraw")]
    public static class Patch_CellBoolDrawer_ActuallyDraw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Quaternion_identity));
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[] {
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_FocusedOnVehicleMap),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_ToBaseMapCoord2),
                new CodeInstruction(OpCodes.Ldc_R4, 0f),
                CodeInstruction.Call(typeof(Vector3Utility), nameof(Vector3Utility.WithY))
            });

            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(MethodInfoCache.g_Quaternion_identity)) + 1;
            var label2 = generator.DefineLabel();
            codes[pos].labels.Add(label2);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),  
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_Rot8_AsQuat),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.o_Quaternion_Multiply)
            });
            return codes;
        }
    }

    //CurrentMapがVehicleMapだったらマップエッジを描くことなんてないよ
    [HarmonyPatch(typeof(GenDraw), "DrawMapEdgeLines")]
    public static class Patch_GenDraw_DrawMapEdgeLines
    {
        public static bool Prefix()
        {
            return !Find.CurrentMap.IsVehicleMapOf(out _);
        }
    }
}