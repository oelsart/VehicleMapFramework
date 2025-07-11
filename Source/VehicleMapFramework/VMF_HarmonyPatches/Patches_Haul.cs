using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaul))]
public static class Patch_HaulAIUtility_PawnCanAutomaticallyHaul
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var num = 0;
        return instructions.Manipulator(c => c.Calls(CachedMethodInfo.g_Thing_Position), c =>
        {
            num++;
            if (num == 2)
            {
                c.opcode = OpCodes.Call;
                c.operand = CachedMethodInfo.m_PositionOnBaseMap;
            }
        });
    }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStorageFor))]
public static class Patch_StoreUtility_TryFindBestBetterStorageFor
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var m_GetSlotGroup = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.GetSlotGroup), [typeof(IntVec3), typeof(Map)]);
        var f_tmpDestMap = AccessTools.Field(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.tmpDestMap));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(m_GetSlotGroup))
            {
                var label = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Ldsfld, f_tmpDestMap);
                yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldsfld, f_tmpDestMap);
                yield return instruction.WithLabels(label);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void Postfix(Pawn carrier, IHaulDestination haulDestination)
    {
        if (haulDestination?.Map != null && carrier != null)
        {
            TargetMapManager.SetTargetMap(carrier, haulDestination.Map);
        }
    }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor))]
public static class Patch_StoreUtility_TryFindBestBetterStoreCellFor
{
    public static void Postfix(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IntVec3 foundCell, bool needAccurateResult, ref bool __result)
    {
        var priority = foundCell.IsValid ? foundCell.GetSlotGroup(map)?.Settings?.Priority ?? currentPriority : currentPriority;
        __result |= StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(t, carrier, map, priority, faction, ref foundCell, needAccurateResult);
    }
}

[HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellForWorker")]
public static class Patch_StoreUtility_TryFindBestBetterStoreCellForWorker
{
    public static bool Prefix(Thing t, Pawn carrier, Map map, Faction faction, ISlotGroup slotGroup, bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared, ref StoragePriority foundPriority)
    {
        Map destMap = null;
        var owner = slotGroup?.Settings?.owner;
        if (owner is StorageGroup storageGroup) destMap = storageGroup.Map;
        else if (owner is IHaulDestination haulDestination) destMap = haulDestination.Map;
        else if (owner is IHaulSource haulSource) destMap = haulSource.Map;
        else if (owner is ISlotGroupParent slotGroupParent) destMap = slotGroupParent.Map;

        if (destMap is not null && destMap != map)
        {
            StoreAcrossMapsUtility.TryFindBestBetterStoreCellForWorker(t, carrier, destMap, faction, slotGroup, needAccurateResult, ref closestSlot, ref closestDistSquared, ref foundPriority);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor))]
public static class Patch_StoreUtility_TryFindBestBetterNonSlotGroupStorageFor
{
    public static void Postfix(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation, ref bool __result)
    {
        var priority = haulDestination is not null ? haulDestination.GetParentStoreSettings()?.Priority ?? currentPriority : currentPriority;
        __result |= StoreAcrossMapsUtility.TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, priority, faction, ref haulDestination, acceptSamePriority, requiresDestReservation);
    }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
public static class Patch_StoreUtility_IsGoodStoreCell
{
    public static bool Prefix(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction, ref bool __result)
    {
        if (map.IsVehicleMapOf(out _))
        {
            __result = StoreAcrossMapsUtility.IsGoodStoreCell(c, map, t, carrier, faction);
            return false;
        }
        return true;
    }
}


[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToCellStorageJob))]
public static class Patch_HaulAIUtility_HaulToCellStorageJob
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatch]
public static class Patch_JobDriver_HaulToCell
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(JobDriver_HaulToCell), nameof(JobDriver_HaulToCell.GetReport));
        yield return AccessTools.FindIncludingInnerTypes(typeof(JobDriver_HaulToCell), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m =>
            {
                if (!m.Name.Contains("<MakeNewToils>")) return false;
                return m.GetMethodBody().LocalVariables.Select(l => l.LocalType).SequenceEqual([typeof(Pawn), typeof(Job), typeof(Thing), typeof(LocalTargetInfo)]);
            });
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var g_JobDriver_Map = AccessTools.PropertyGetter(typeof(JobDriver), "Map");
        var m_TargetMapOrPawnMap = AccessTools.Method(typeof(Patch_JobDriver_HaulToCell), nameof(TargetMapOrPawnMap));
        return instructions.MethodReplacer(g_JobDriver_Map, m_TargetMapOrPawnMap);
    }

    public static Map TargetMapOrPawnMap(JobDriver instance)
    {
        if (TargetMapManager.HasTargetMap(instance.pawn, out var map))
        {
            return map;
        }
        return instance.pawn.MapHeld;
    }
}

[HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.CarryHauledThingToCell))]
public static class Patch_Toils_Haul_CarryHauledThingToCell
{
    public static void Postfix(Toil __result, TargetIndex squareIndex, PathEndMode pathEndMode)
    {
        __result.AddPreInitAction(() =>
        {
            var actor = __result.actor;
            if (!TargetMapManager.HasTargetMap(actor, out var map) || actor.Map == map)
            {
                return;
            }
            var target = actor.jobs.curJob.GetTarget(squareIndex);
            if (actor.CanReach(target, pathEndMode, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot);
                TargetMapManager.RemoveTargetInfo(actor);
            }
        });
    }
}

[HarmonyPatch]
public static class Patch_Toils_Haul_IsValidStorageFor
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var t in AccessTools.InnerTypes(typeof(Toils_Haul)))
        {
            var methods = t.GetDeclaredMethods();
            var method = methods.FirstOrDefault(m =>
            {
                if (m.Name.Contains("DupeValidator"))
                {
                    return true;
                }
                if (!m.Name.Contains("<CarryHauledThingToCell>")) return false;
                return m.GetMethodBody().LocalVariables.Select(l => l.LocalType).SequenceEqual([typeof(Pawn), typeof(IntVec3), typeof(CompPushable), typeof(LocalTargetInfo)]);
            });
            if (method != null) yield return method;
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}
