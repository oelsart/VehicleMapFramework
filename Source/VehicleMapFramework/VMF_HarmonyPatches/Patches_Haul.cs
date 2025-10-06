using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaul))]
[PatchLevel(Level.Sensitive)]
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
    [PatchLevel(Level.Sensitive)]
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

    [PatchLevel(Level.Safe)]
    public static void Postfix(Pawn carrier, IHaulDestination haulDestination, IntVec3 foundCell)
    {
        if (haulDestination?.Map != null)
        {
            TargetMapManager.SetTargetInfo(carrier, new TargetInfo(foundCell, haulDestination.Map));
        }
    }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor))]
[PatchLevel(Level.Safe)]
public static class Patch_StoreUtility_TryFindBestBetterStoreCellFor
{
    public static void Postfix(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IntVec3 foundCell, bool needAccurateResult, ref bool __result)
    {
        var priority = foundCell.IsValid ? foundCell.GetSlotGroup(map)?.Settings?.Priority ?? currentPriority : currentPriority;
        __result |= StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(t, carrier, map, priority, faction, ref foundCell, needAccurateResult);
        if (StoreAcrossMapsUtility.tmpDestMap != null)
        {
            TargetMapManager.SetTargetInfo(carrier, new TargetInfo(foundCell, StoreAcrossMapsUtility.tmpDestMap));
        }
    }
}

[HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellForWorker")]
[PatchLevel(Level.Safe)]
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
[PatchLevel(Level.Safe)]
public static class Patch_StoreUtility_TryFindBestBetterNonSlotGroupStorageFor
{
    public static void Postfix(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation, ref bool __result)
    {
        var priority = haulDestination is not null ? haulDestination.GetParentStoreSettings()?.Priority ?? currentPriority : currentPriority;
        __result |= StoreAcrossMapsUtility.TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, priority, faction, ref haulDestination, acceptSamePriority, requiresDestReservation);
    }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
[PatchLevel(Level.Safe)]
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
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
    }

    [HarmonyBefore(ModCompat.StackGap.HarmonyId)]
    [PatchLevel(Level.Safe)]
    public static void Postfix(Pawn p, IntVec3 storeCell, Job __result)
    {
        if (TargetMapManager.HasTargetMap(p, out var map))
        {
            __result?.globalTarget = new GlobalTargetInfo(storeCell, map);  
        }
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobDriver_HaulToCell
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(JobDriver_HaulToCell), nameof(JobDriver_HaulToCell.GetReport));
        yield return AccessTools.FindIncludingInnerTypes(typeof(JobDriver_HaulToCell), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m =>
            {
                return m.Name.Contains("<MakeNewToils>") &&
                       m.GetMethodBody()!.LocalVariables
                           .Select(l => l.LocalType)
                           .SequenceEqual([typeof(Pawn), typeof(Job), typeof(Thing), typeof(LocalTargetInfo)]);
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
        if (TargetMapManager.HasTargetMap(instance.pawn, out var map) || (map = instance.job.globalTarget.Map) != null || (map = instance.job.targetA.Thing?.MapHeld) != null)
        {
            return map;
        }
        return instance.pawn.MapHeld;
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
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
                return m.Name.Contains("<CarryHauledThingToCell>") &&
                       m.GetMethodBody()!.LocalVariables
                           .Select(l => l.LocalType)
                           .SequenceEqual([typeof(Pawn), typeof(IntVec3), typeof(CompPushable), typeof(LocalTargetInfo)]);
            });
            if (method != null) yield return method;
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
    }
}

[HarmonyPatch(typeof(LoadTransportersJobUtility), nameof(LoadTransportersJobUtility.FindThingToLoad))]
public static class Patch_LoadTransportersJobUtility_FindThingToLoad
{
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    [PatchLevel(Level.Mandatory)]
    public static ThingCount FindThingToLoad(Pawn p, CompTransporter transporter)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new CodeMatcher(instructions);
            codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position));
            codes.Insert(
                new CodeInstruction(OpCodes.Pop),
                CodeInstruction.LoadArgument(1),
                CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)));

            codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
            codes.Insert(
                new CodeInstruction(OpCodes.Pop),
                CodeInstruction.LoadArgument(1),
                CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)));

            var m_ClosestThingReachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
            var m_ClosestThingReachableOriginal = AccessTools.Method(typeof(Patch_GenClosest_ClosestThingReachable), nameof(Patch_GenClosest_ClosestThingReachable.ClosestThingReachableOriginal));
            return codes.Instructions().MethodReplacer(m_ClosestThingReachable, m_ClosestThingReachableOriginal);
        }
    }

    [PatchLevel(Level.Safe)]
    public static bool Prefix(Pawn p, CompTransporter transporter, ref ThingCount __result)
    {
        if (transporter is CompBuildableContainer { GatherFromBaseMap: false })
        {
            __result = FindThingToLoad(p, transporter);
            return false;
        }
        return true;
    }
}