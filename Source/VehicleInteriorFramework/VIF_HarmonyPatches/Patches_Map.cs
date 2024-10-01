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

    //このクラスのCurrentMapにオリジナルのFind.CurrentMapを保存して、Find.CurrentMapはフォーカスされたマップがある場合そっちを参照するように改変
    [HarmonyPatch(typeof(Find), nameof(Find.CurrentMap), MethodType.Getter)]
    public static class Patch_Find_CurrentMap
    {
        [HarmonyReversePatch(HarmonyReversePatchType.Original)]
        public static Map CurrentMap() => throw new NotImplementedException();

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

    [HarmonyPatch(typeof(Game), nameof(Game.CurrentMap), MethodType.Setter)]
    public static class Patch_Game_CurrentMap
    {
        public static void Prefix(ref Map value)
        {
            VehicleMapUtility.FocusedVehicle = null;
            if (value.Parent is MapParent_Vehicle parentVehicle)
            {
                value = parentVehicle.vehicle.Map;
            }
        }
    }

    //MapUpdateはFocusedVehicleがあってもやってほしいので保存しておいた元のCurrentMapを使わせる
    [HarmonyPatch(typeof(Map), nameof(Map.MapUpdate))]
    public static class Patch_Map_MapUpdate
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var from = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
            var to = AccessTools.Method(typeof(Patch_Find_CurrentMap), nameof(Patch_Find_CurrentMap.CurrentMap));
            return instructions.MethodReplacer(from, to);
        }
    }

    //カメラも同様
    [HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Update))]
    public static class Patch_CameraDriver_Update
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Map_MapUpdate.Transpiler(instructions);
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
