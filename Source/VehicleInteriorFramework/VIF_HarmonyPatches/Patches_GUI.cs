using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
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
            var vehicles = VehiclePawnWithMapCache.allVehicles[Find.CurrentMap];
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

    //VehicleMapはコロニストバーに表示させない
    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class Patch_ColonistBar_CheckRecacheEntries
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var getMaps = AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getMaps)) + 1;
            codes.Insert(pos, CodeInstruction.Call(typeof(Patch_ColonistBar_CheckRecacheEntries), nameof(Patch_ColonistBar_CheckRecacheEntries.ExcludeVehicleMaps)));

            var m_PlayerPawnsDisplayOrderUtility_Sort = AccessTools.Method(typeof(PlayerPawnsDisplayOrderUtility), nameof(PlayerPawnsDisplayOrderUtility.Sort));
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(m_PlayerPawnsDisplayOrderUtility_Sort));
            codes.InsertRange(pos2, new[]
            {
                CodeInstruction.LoadField(typeof(ColonistBar), "tmpMaps"),
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<Map>), "Item")),
                CodeInstruction.Call(typeof(Patch_ColonistBar_CheckRecacheEntries), nameof(Patch_ColonistBar_CheckRecacheEntries.IncludeVehicleMapPawns))
            });
            return codes;
        }

        private static IEnumerable<Map> ExcludeVehicleMaps(this IEnumerable<Map> maps)
        {
            return maps.Where(m => !m.IsVehicleMapOf(out var vehicle) || !vehicle.Spawned);
        }

        private static List<Pawn> IncludeVehicleMapPawns(List<Pawn> tmpPawns, Map map)
        {
            var allVehicles = VehiclePawnWithMapCache.allVehicles[map];
            tmpPawns.AddRange(allVehicles.SelectMany(v => v.interiorMap.mapPawns.FreeColonists));
            tmpPawns.AddRange(allVehicles.SelectMany(v => v.interiorMap.mapPawns.ColonyMutantsPlayerControlled));
            foreach(var corpse in allVehicles.SelectMany(v => v.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)))
            {
                if (corpse.IsDessicated()) continue;
                var innerPawn = ((Corpse)corpse).InnerPawn;
                if (innerPawn != null && innerPawn.IsColonist)
                {
                    tmpPawns.Add(innerPawn);
                }
            }
            foreach(var pawn in allVehicles.SelectMany(v => v.interiorMap.mapPawns.AllPawnsSpawned))
            {
                if (pawn.carryTracker.CarriedThing is Corpse corpse && !corpse.IsDessicated() && corpse.InnerPawn.IsColonist)
                {
                    tmpPawns.Add(corpse.InnerPawn);
                }
            }
            return tmpPawns;
        }
    }
}
