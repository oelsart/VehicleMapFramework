using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
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
            var m_CellRect_ClipInsideMap = AccessTools.Method(typeof(CellRect), nameof(CellRect.ClipInsideMap));
            var m_ClipInsideVehicleMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ClipInsideVehicleMap));
            return instructions.MethodReplacer(m_CellRect_ClipInsideMap, m_ClipInsideVehicleMap);
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class Patch_MechanitorUtility_InMechanitorCommandRange
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
    public static class Patch_Pawn_MechanitorTracker_CanCommandTo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.Reserve))]
    public static class Patch_ReservationManager_Reserve
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ReservationManager_CanReserve = AccessTools.Method(typeof(ReservationManager), nameof(ReservationManager.CanReserve));
            var m_ReservationAcrossMapsUtility_CanReserve = AccessTools.Method(typeof(ReservationAcrossMapsUtility), nameof(ReservationAcrossMapsUtility.CanReserve),
                new Type[] { typeof(ReservationManager), typeof(Pawn), typeof(LocalTargetInfo), typeof(int), typeof(int), typeof(ReservationLayerDef), typeof(bool), typeof(Map) });
            var f_ReservationManager_map = AccessTools.Field(typeof(ReservationManager), "map");

            foreach(var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.OperandIs(m_ReservationManager_CanReserve))
                {
                    yield return CodeInstruction.LoadArgument(0);
                    yield return new CodeInstruction(OpCodes.Ldfld, f_ReservationManager_map);
                    yield return new CodeInstruction(OpCodes.Call, m_ReservationAcrossMapsUtility_CanReserve);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserveStack))]
    public static class Patch_ReservationManager_CanReserveStack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing);

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
            codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Map));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
    public static class Patch_Reachability_CanReach
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing);

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
            codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Map));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachMapEdge), typeof(IntVec3), typeof(TraverseParms))]
    public static class Patch_Reachability_CanReachMapEdge
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing);

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
            codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Map));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.EffectiveAreaRestrictionInPawnCurrentMap), MethodType.Getter)]
    public static class Patch_Pawn_PlayerSettings_EffectiveAreaRestrictionInPawnCurrentMap
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }

    ////ストレージの優先度変更の時ベースマップや他の車両マップのlisterHaulablesにも通知
    //[HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.Priority), MethodType.Setter)]
    //public static class Patch_StorageSettings_Priority
    //{
    //    public static void Postfix(StorageSettings __instance)
    //    {
    //        if (Current.ProgramState != ProgramState.Playing)
    //        {
    //            return;
    //        }
    //        if (__instance.owner is StorageGroup storageGroup && storageGroup.Map != null)
    //        {
    //            var baseMap = storageGroup.Map.BaseMap();
    //            foreach (var map in VehiclePawnWithMapCache.allVehicles[baseMap].Select(v => v.interiorMap).Concat(baseMap).Where(m => m != storageGroup.Map))
    //            {
    //                map.listerHaulables.RecalculateAllInHaulSources(storageGroup.HaulSourcesList);
    //            }
    //        }
    //    }
    //}

    //主にlisterHaulablesの再計算の時のチェックでベースマップや他の車両マップを検索対象に含めるためメソッドを置き換え
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsInValidBestStorage))]
    public static class Patch_StoreUtility_IsInValidBestStorage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_StoreUtility_TryFindBestBetterStorageFor = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStorageFor));
            var m_StoreAcrossMapsUtility_TryFindBestBetterStorageFor = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.TryFindBestBetterStorageFor),
                new Type[] { typeof(Thing), typeof(Pawn), typeof(Map), typeof(StoragePriority), typeof(Faction), typeof(IntVec3).MakeByRefType(), typeof(IHaulDestination).MakeByRefType(), typeof(bool) });
            return instructions.MethodReplacer(m_StoreUtility_TryFindBestBetterStorageFor, m_StoreAcrossMapsUtility_TryFindBestBetterStorageFor);
        }
    }
}
