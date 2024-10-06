using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    //VehicleMapはコロニストバーに表示させない
    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class Patch_ColonistBar_CheckRecacheEntries
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var getMaps = AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getMaps)) + 1;
            codes.Insert(pos, CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ExceptVehicleMaps)));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Designator), nameof(Designator.Map), MethodType.Getter)]
    public static class Patch_Designator_Map
    {
        public static bool Prefix(ref Map __result)
        {
            if (VehicleMapUtility.FocusedVehicle != null)
            {
                __result = VehicleMapUtility.FocusedVehicle.interiorMap;
                return false;
            }
            return true;
        }
    }

    //VehicleMapの時はSectionLayer_VehicleMapの継承クラスを使い、そうでなければそれらは除外する
    [HarmonyPatch(typeof(Section), MethodType.Constructor, typeof(IntVec3), typeof(Map))]
    public static class Patch_Section_Constructor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var getAllSubclassesNA = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllSubclassesNonAbstract));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getAllSubclassesNA)) + 1;

            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(2),
                CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SelectSectionLayers))
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(DynamicDrawManager), "ComputeCulledThings")]
    public static class Patch_DynamicDrawManager_ComputeCulledThings
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var expandedBy = AccessTools.Method(typeof(CellRect), nameof(CellRect.ExpandedBy));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(expandedBy)) + 1;
            var label = generator.DefineLabel();
            var parentVehicle = generator.DeclareLocal(typeof(MapParent_Vehicle));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[] {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(DynamicDrawManager), "map"),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Map), nameof(Map.Parent))),
                new CodeInstruction(OpCodes.Isinst, typeof(MapParent_Vehicle)),
                new CodeInstruction(OpCodes.Stloc_S, parentVehicle),
                new CodeInstruction(OpCodes.Ldloc_S, parentVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, parentVehicle),
                CodeInstruction.LoadField(typeof(MapParent_Vehicle), nameof(MapParent_Vehicle.vehicle)),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[]{ typeof(CellRect), typeof(VehiclePawnWithInterior) }))
            });
            return codes;
        }
    }
}
