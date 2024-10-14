using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using Verse;
using System;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(WorldInterface), nameof(WorldInterface.WorldInterfaceOnGUI))]
    public static class Patch_WorldInterface_WorldInterfaceOnGUI
    {
        public static void Postfix()
        {
            if (VehicleMapUtility.FocusedVehicle != null && !Find.WindowStack.IsOpen<MainTabWindow>() && !WorldRendererUtility.WorldRenderedNow)
            {
                GizmoGridDrawer.DrawGizmoGrid(new List<Gizmo> { new Command_FocusVehicleMap() }, 324f, out var mouseoverGizmo);
            }
        }

        [HarmonyPatch(typeof(Graphic), nameof(Graphic.Draw))]
        public static class Patch_Graphic_Draw
        {
            public static void Prefix(Thing thing, ref float extraRotation)
            {
                if (thing.IsOnVehicleMapOf(out var vehicle))
                {
                    extraRotation += vehicle.CachedAngle;
                }
            }
        }

        [HarmonyPatch(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI))]
        public static class Patch_ThingOverlays_ThingOverlaysOnGUI
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap).ToList();
                var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);

                codes.Insert(pos, CodeInstruction.Call(typeof(Patch_ThingOverlays_ThingOverlaysOnGUI), nameof(Patch_ThingOverlays_ThingOverlaysOnGUI.IncludeVehicleMapThings)));

                return codes;
            }

            public static List<Thing> IncludeVehicleMapThings(List<Thing> list)
            {
                var vehicles = Find.CurrentMap.listerThings.GetThingsOfType<VehiclePawnWithInterior>();
                var result = new List<Thing>(list);
                foreach (var vehicle in vehicles)
                {
                    result.AddRange(vehicle.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay));
                }
                return result;
            }
        }

        [HarmonyPatch(typeof(Designation), nameof(Designation.DrawLoc))]
        public static class Patch_Designation_DrawLoc
        {
            public static void Postfix(Designation __instance, ref Vector3 __result)
            {
                if (__instance.designationManager.map.Parent is MapParent_Vehicle)
                {
                    __result.y += VehicleMapUtility.altitudeOffsetFull;
                }
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
                codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap1));

                var label = generator.DefineLabel();
                var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldsfld && c.OperandIs(field));
                codes[pos2].labels.Add(label);
                codes.InsertRange(pos2, new[]
                {
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedVehicle))),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedVehicle))),
            new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Rot8Utility), nameof(Rot8Utility.AsQuat))),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Quaternion), "op_Multiply", new Type[]{ typeof(Quaternion), typeof(Quaternion) })),
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
    }
}
