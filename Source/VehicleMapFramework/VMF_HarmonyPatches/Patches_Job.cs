using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Pawn_JobTracker_StartJob
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_MakeDriver = AccessTools.Method(typeof(Job), nameof(Job.MakeDriver));
        var m_MakeOrGetDriver = AccessTools.Method(typeof(Patch_Pawn_JobTracker_StartJob), nameof(MakeOrGetDriver));
        return instructions.MethodReplacer(m_MakeDriver, m_MakeOrGetDriver);
    }

    private static JobDriver MakeOrGetDriver(Job curJob, Pawn driverPawn)
    {
        if (typeof(JobDriverAcrossMaps).IsAssignableFrom(curJob.def.driverClass) || curJob.jobGiver?.GetType() == typeof(JobDriver_GotoDestMap.ThinkNode_JobFromGotoDestMap))
        {
            return curJob.GetCachedDriver(driverPawn);
        }
        return curJob.MakeDriver(driverPawn);
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Pawn_JobTracker_DetermineNextJob
{
    public static void Prefix(Pawn ___pawn, bool ignoreQueue)
    {
        if (!ignoreQueue && ___pawn.jobs.jobQueue.Any()) return;
        TargetMapManager.RemoveTargetInfo(___pawn);
    }
}

[HarmonyAfter("SmarterConstruction")]
[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobGiver_Work_TryIssueJobPackage
{
    private static readonly List<Map> tmpMaps = [];
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
    {
        var codes = new CodeMatcher(instructions, generator);
        //scanner変数をローカルに保存しておく
        codes.MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Isinst && c.operand.Equals(typeof(WorkGiver_Scanner))));
        codes.DeclareLocal(typeof(WorkGiver_Scanner), out var scanner);
        codes.InsertAfterAndAdvance(
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Stloc_S, scanner));

        object local = null;
        var matchStloc = new CodeMatch(c =>
            c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalType == typeof(IEnumerable<Thing>) &&
            // ReSharper disable once AccessToModifiedClosure
            c.operand != local);
        var addedCodes = new[]
        {
            CodeInstruction.LoadArgument(1),
            new CodeInstruction(OpCodes.Ldloc_S, scanner),
            CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(AddSearchSet))
        };
        codes.MatchStartForward(matchStloc);
        local = codes.Operand;

        //サーチセットに複数マップのthingリストを足す
        codes.MatchStartForward(matchStloc);
        local = codes.Operand;
        codes.InsertAndAdvance(addedCodes);

        codes.MatchStartForward(matchStloc);
        codes.InsertAndAdvance(addedCodes);

        //複数マップのセルをスキャンする
        codes.MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalType == typeof(IEnumerable<IntVec3>)));
        var locals = original.GetMethodBody()?.LocalVariables;
        var innerTypeIndex = locals.FirstIndexOf(l => l.LocalType?.GetCustomAttribute<CompilerGeneratedAttribute>() != null); //たぶん0のはずだけど一応
        var innerStructIndex = locals.FirstIndexOf(l => l.LocalType?.GetCustomAttribute<CompilerGeneratedAttribute>() != null && l.LocalType.IsStruct());
        codes.InsertAfterAndAdvance(
            new CodeInstruction(OpCodes.Ldloc_S, scanner),
            CodeInstruction.LoadLocal(innerTypeIndex, true),
            CodeInstruction.LoadLocal(innerStructIndex, true),
            CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(ScanCellsAcrossMaps)));

        var g_TargetInfo_Cell = AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Cell));
        codes.MatchStartForward(CodeMatch.Calls(g_TargetInfo_Cell));
        codes.RemoveInstruction();

        var m_JobOnCell = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnCell));
        codes.MatchStartForward(CodeMatch.Calls(m_JobOnCell));
        codes.SetInstruction(CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(JobOnCellMap)));

        //GenClosestの各メソッドを自作のものに置き換える
        //PotentialWorkThingsGlobalの各マップの結果を合計
        var m_GenClosest_ClosestThing_Global = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
        var m_GenClosestCrossMap_ClosestThing_Global = AccessTools.Method(typeof(GenClosestCrossMap), nameof(GenClosestCrossMap.ClosestThing_Global));
        var m_GenClosest_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
        var m_GenClosestCrossMap_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosestCrossMap), nameof(GenClosestCrossMap.ClosestThing_Global_Reachable));
        var m_Scanner_PotentialWorkThingsGlobal = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal));
        var m_PotentialWorkThingsGlobalAll = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(PotentialWorkThingsGlobalAll));
        var m_Scanner_JobOnThing = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnThing));
        var m_JobOnThingMap = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(JobOnThingMap));
        return codes.Instructions()
            .MethodReplacer(m_GenClosest_ClosestThing_Global, m_GenClosestCrossMap_ClosestThing_Global)
            .MethodReplacer(m_GenClosest_ClosestThing_Global_Reachable, m_GenClosestCrossMap_ClosestThing_Global_Reachable)
            .MethodReplacer(m_Scanner_PotentialWorkThingsGlobal, m_PotentialWorkThingsGlobalAll)
            .MethodReplacer(m_Scanner_JobOnThing, m_JobOnThingMap);
    }

    internal static IEnumerable<Thing> AddSearchSet(List<Thing> list, Pawn pawn, WorkGiver_Scanner scanner)
    {
        if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, null))
        {
            return list;
        }

        tmpMaps.Clear();
        tmpMaps.AddRange(pawn.Map.BaseMapAndVehicleMaps().Except(pawn.Map));
        return tmpMaps.Any() ? tmpMaps.SelectMany(m => m.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest)).ConcatIfNotNull(list).Distinct() : list;
    }

    extension(WorkGiver_Scanner scanner)
    {
        internal IEnumerable<Thing> PotentialWorkThingsGlobalAll(Pawn pawn)
        {
            if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, null))
            {
                return scanner.PotentialWorkThingsGlobal(pawn);
            }
            var map = pawn.Map;
            pawn.DepartMap = map;
            var pos = pawn.Position;
            try
            {
                IEnumerable<Thing> enumerable = null;
                pawn.Map.BaseMapAndVehicleMaps().Do(m =>
                {
                    pawn.VirtualMapTransfer(m);
                    var things = scanner.PotentialWorkThingsGlobal(pawn)?.ToArray();
                    if (enumerable == null)
                    {
                        enumerable = things;
                    }
                    else if (things != null)
                    {
                        enumerable = enumerable.Concat(things);
                    }
                });
                return enumerable?.Distinct();
            }
            finally
            {
                pawn.VirtualMapTransfer(map, pos);
                pawn.RemoveDepartMap();
            }
        }

        internal Job JobOnThingMap(Pawn pawn, Thing t, bool forced = false)
        {
            var thingMap = t.MapHeld;
            if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, thingMap))
            {
                return scanner.JobOnThing(pawn, t, forced);
            }

            var map = pawn.Map;
            pawn.DepartMap = map;
            pawn.VirtualMapTransfer(thingMap);
            try
            {
                return scanner.JobOnThing(pawn, t, forced);
            }
            finally
            {
                pawn.VirtualMapTransfer(map);
                pawn.RemoveDepartMap();
            }
        }

        internal Job JobOnCellMap(Pawn pawn, in TargetInfo target, bool forced = false)
        {
            var map = pawn.Map;
            var targetMap = target.Map;
            if (map == targetMap)
            {
                return scanner.JobOnCell(pawn, target.Cell, forced);
            }

            if (pawn.CanReach(target.Cell, scanner.PathEndMode, scanner.MaxPathDanger(pawn), false, false,
                    TraverseMode.ByPawn, targetMap, out var exitSpot, out var enterSpot, out var spotsQueue))
            {
                using (new VirtualTeleporter(pawn, targetMap, enterSpot.Cell))
                {
                    return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue, scanner.JobOnCell(pawn, target.Cell, forced));
                }
            }

            if (!CrossMapReachabilityUtility.GetClosestExitEnterSpot(map, pawn.Position, TraverseParms.For(pawn), targetMap,
                    out var exitSpot2, out var enterSpot2, out var spotsQueue2)) return null;
            Job job;
            try
            {
                pawn.DepartMap = map;
                pawn.DestMap = targetMap;
                pawn.VirtualMapTransfer(targetMap);
                job = scanner.JobOnCell(pawn, target.Cell, forced);
            }
            finally
            {
                pawn.RemoveDepartMap();
                pawn.RemoveDestMap();
                pawn.VirtualMapTransfer(map);
            }
            return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot2, enterSpot2, spotsQueue2, job);
        }

        internal void ScanCellsAcrossMaps(ref InnerClass innerClass, ref InnerStruct innerStruct)
        {
            var pawn = innerClass.pawn;
            var basePos = pawn.PositionOnBaseMap();
            var map = pawn.DepartMap = pawn.Map;
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            try
            {
                foreach (var map2 in maps)
                {
                    pawn.VirtualMapTransfer(map2);
                    var positionOnMap = map2.IsVehicleMapOf(out var vehicle) ? basePos.ToVehicleMapCoord(vehicle) : basePos;
                    var enumerable2 = scanner.PotentialWorkCellsGlobal(pawn);
                    foreach (var c in enumerable2)
                    {
                        var flag2 = false;
                        float num4 = (c - positionOnMap).LengthHorizontalSquared;
                        var num5 = 0f;
                        if (innerStruct.prioritized)
                        {
                            if (!c.IsForbidden(pawn, map2) && scanner.HasJobOnCell(pawn, c))
                            {
                                num5 = scanner.GetPriority(pawn, c);
                                if (num5 > innerStruct.bestPriority || (Mathf.Approximately(num5, innerStruct.bestPriority) && num4 < innerStruct.closestDistSquared))
                                {
                                    flag2 = true;
                                }
                            }
                        }
                        else if (num4 < innerStruct.closestDistSquared && !c.IsForbidden(pawn, map2) && scanner.HasJobOnCell(pawn, c))
                        {
                            flag2 = true;
                        }

                        if (!flag2) continue;
                        innerClass.bestTargetOfLastPriority = new TargetInfo(c, map2);
                        innerClass.scannerWhoProvidedTarget = scanner;
                        innerStruct.closestDistSquared = num4;
                        innerStruct.bestPriority = num5;
                    }
                }
            }
            finally
            {
                pawn.VirtualMapTransfer(map);
                pawn.RemoveDepartMap();
            }
        }
    }

    public struct InnerStruct
    {
        public IntVec3 pawnPosition;

        public bool prioritized;

        public bool allowUnreachable;

        public Danger maxPathDanger;

        public float bestPriority;

        public float closestDistSquared;
    }

    public class InnerClass
    {
        public Pawn pawn;

        public TargetInfo bestTargetOfLastPriority;

        public WorkGiver_Scanner scannerWhoProvidedTarget;
    }
}

//ShouldSkipはvehicleMapを含めた全てのマップでスキップするかチェックする
[HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobGiver_Work_PawnCanUseWorkGiver
{
    public static readonly HashSet<Type> NoNeedVirtualMapTransferList = [];
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_WorkGiver_ShouldSkip = AccessTools.Method(typeof(WorkGiver), nameof(WorkGiver.ShouldSkip));
        var m_ShouldSkipAll = AccessTools.Method(typeof(Patch_JobGiver_Work_PawnCanUseWorkGiver), nameof(ShouldSkipAll));
        return instructions.MethodReplacer(m_WorkGiver_ShouldSkip, m_ShouldSkipAll);
    }

    public static bool ShouldSkipAll(this WorkGiver workGiver, Pawn pawn, bool forced = false)
    {
        if (NoNeedVirtualMapTransferList.Contains(workGiver.GetType()))
        {
            return workGiver.ShouldSkip(pawn, forced);
        }
        return pawn.Map.BaseMapAndVehicleMaps().All(m =>
        {
            using var _ = new VirtualTeleporter(pawn, m);
            return workGiver.ShouldSkip(pawn, forced);
        });
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobGiver_Work_Validator
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.InnerTypes(typeof(JobGiver_Work)).SelectMany(t => t.GetDeclaredMethods()).First(m => m.Name.Contains("Validator"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_Scanner_HasJobOnThing = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.HasJobOnThing));
        var m_HasJobOnThingMap = AccessTools.Method(typeof(Patch_JobGiver_Work_Validator), nameof(HasJobOnThingMap));
        return instructions.MethodReplacer(m_Scanner_HasJobOnThing, m_HasJobOnThingMap);
    }

    //目的のtに届く位置とマップに転移してからHasJobOnThingを走らせる
    public static bool HasJobOnThingMap(this WorkGiver_Scanner scanner, Pawn pawn, Thing t, bool forced = false)
    {
        var thingMap = t.MapHeld;
        if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, thingMap))
        {
            return scanner.HasJobOnThing(pawn, t, forced);
        }

        var map = pawn.Map;
        pawn.DepartMap = map;
        using var _ = new VirtualTeleporter(pawn, thingMap);
        try
        {
            return scanner.HasJobOnThing(pawn, t, forced);
        }
        finally
        {
            pawn.RemoveDepartMap();
        }
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobGiver_Work_GiverTryGiveJobPrioritized
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(JobGiver_Work), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<GiverTryGiveJobPrioritized>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_JobGiver_Work_Validator.Transpiler(instructions);
}

[HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
[PatchLevel(Level.Safe)]
public static class Patch_Pawn_PathFollower_StartPath
{
    public static bool Prefix(LocalTargetInfo dest, PathEndMode peMode, Pawn ___pawn)
    {
        if (___pawn.jobs is null or {curDriver: JobDriver_GotoAcrossMaps }) return true;

        var flag = false;
        var destMap = dest.Thing?.MapHeld;
        if (destMap is null)
        {
            flag = true;
            destMap = (TargetMapManager.HasTargetInfo(___pawn, out var target) || (target = (TargetInfo)___pawn.CurJob.globalTarget).IsValid) && (LocalTargetInfo)target == dest ? target.Map : null;
        }
        if (destMap is null)
        {
            return true;
        }
        if (___pawn.Map != destMap && ___pawn.CanReach(dest, peMode, Danger.Deadly, false, false, TraverseMode.ByPawn,
                destMap, out var exitSpot, out var enterSpot, out var spotsQueue))
        {
            if (flag)
            {
                TargetMapManager.RemoveTargetInfo(___pawn);
                ___pawn.CurJob.globalTarget = GlobalTargetInfo.Invalid;
            }
            JobAcrossMapsUtility.StartGotoDestMapJob(___pawn, exitSpot, enterSpot, spotsQueue);
            return false;
        }
        return true;
    }
}

//targetにThingが入ってるのにGotoCellを使ってるようなケースでは先にマップが違うかどうかチェックする
[HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), typeof(IntVec3), typeof(PathEndMode))]
[PatchLevel(Level.Safe)]
public static class Patch_Toils_Goto_GotoCell
{
    public static void Postfix(IntVec3 cell, PathEndMode peMode, Toil __result)
    {
        __result.AddPreInitAction(() =>
        {
            var actor = __result.actor;
            var curJob = actor.CurJob;
            var allTargets = new[] { curJob.targetA, curJob.targetB, curJob.targetC }.ConcatIfNotNull(curJob.targetQueueA).ConcatIfNotNull(curJob.targetQueueB);
            var target = allTargets.FirstOrFallback(t => t.HasThing && (t.Cell == cell || (t.Thing.Spawned && t.Thing.InteractionCell == cell)), LocalTargetInfo.Invalid);
            if (target.IsValid && actor.Map != target.Thing.MapHeld && actor.CanReach(target, peMode, Danger.Deadly,
                    false, false, TraverseMode.ByPawn, target.Thing.MapHeld, out var exitSpot, out var enterSpot,
                    out var spotsQueue))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot, spotsQueue);
            }
        });
    }
}

[HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoBuild))]
[PatchLevel(Level.Safe)]
public static class Patch_Toils_Goto_GotoBuild
{
    public static void Postfix(TargetIndex ind, Toil __result)
    {
        __result.AddPreInitAction(() =>
        {
            var actor = __result.actor;
            var curJob = actor.CurJob;
            var target = curJob.GetTarget(ind);
            var thingMap = target.Thing?.MapHeld;
            if (thingMap != null && actor.Map != thingMap && actor.CanReach(target, PathEndMode.Touch, Danger.Deadly,
                    false, false, TraverseMode.ByPawn, thingMap, out var exitSpot, out var enterSpot,
                    out var spotsQueue))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot, spotsQueue);
            }
        });
    }
}

//GotoCellと同じやり方でSittableOrSpotのチェック
[HarmonyPatch(typeof(ReservationUtility), nameof(ReservationUtility.ReserveSittableOrSpot))]
[PatchLevel(Level.Safe)]
public static class Patch_ReservationUtility_ReserveSittableOrSpot
{
    public static bool Prefix(Pawn pawn, IntVec3 exactSittingPos, Job job, ref Map __state)
    {
        Map map;
        if (job?.targetA.Thing?.Map != null && job.targetA.Thing.def.hasInteractionCell && job.targetA.Thing.InteractionCell == exactSittingPos)
            map = job.targetA.Thing.Map;
        else
            map = job?.globalTarget.Map ?? TargetMapManager.TargetMapOrPawnMap(pawn);

        if (map is null)
        {
            return true;
        }
        if (pawn.Map != map)
        {
            __state = pawn.Map;
            pawn.VirtualMapTransfer(map);
        }
        return exactSittingPos.InBounds(map);
    }

    public static void Finalizer(Pawn pawn, IntVec3 exactSittingPos, Job job, Map __state, bool __result)
    {
        if (__state != null)
        {
            var destMap = pawn.Map;
            pawn.VirtualMapTransfer(__state);
            if (__result)
            {
                job.globalTarget = new GlobalTargetInfo(exactSittingPos, destMap);
            }
        }
    }
}

//GotoCellと同じやり方でSittableOrSpotのチェック
[HarmonyPatch(typeof(ReservationUtility), nameof(ReservationUtility.CanReserveSittableOrSpot), typeof(Pawn), typeof(IntVec3), typeof(Thing), typeof(bool))]
[PatchLevel(Level.Safe)]
public static class Patch_ReservationUtility_CanReserveSittableOrSpot
{
    public static bool Prefix(Pawn pawn, IntVec3 exactSittingPos, Thing ignoreThing, ref Map __state)
    {
        if (pawn?.Map is null)
            return false;
        
        var map = ignoreThing?.Map ?? TargetMapManager.TargetMapOrPawnMap(pawn);
        if (map is null)
            return true;
        if (pawn.Map != map)
        {
            __state = pawn.Map;
            pawn.VirtualMapTransfer(map);
        }
        return exactSittingPos.InBounds(map);
    }

    public static void Finalizer(Pawn pawn, Map __state)
    {
        if (__state != null)
        {
            pawn.VirtualMapTransfer(__state);
        }
    }
}

[HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TryFindFreeSittingSpotOnThing))]
[PatchLevel(Level.Safe)]
public static class Patch_Toils_Ingest_TryFindFreeSittingSpotOnThing
{
    public static void Prefix(Thing t, Pawn pawn)
    {
        if (pawn.Map != t.Map && t.Map != null)
        {
            pawn.CurJob?.globalTarget = t;
        }
    }
}

[HarmonyPatch(typeof(Toils_Bed), nameof(Toils_Bed.GotoBed))]
[PatchLevel(Level.Safe)]
public static class Patch_Toils_Bed_GotoBed
{
    public static void Postfix(TargetIndex bedIndex, Toil __result)
    {
        //Bunk BedsのFailOn処理であらかじめベッドの位置にポジションを変更しておりマップが違う場合にエラーとなるため、endConditionsの先頭でチェックする
        __result.endConditions.Insert(0, () =>
        {
            var pawn = __result.actor;
            var bed = pawn.CurJob.GetTarget(bedIndex).Thing;
            if (pawn.Map != bed.Map && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Deadly, false, false,
                    TraverseMode.ByPawn, bed.Map, out var exitSpot, out var enterSpot, out var spotsQueue))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue);
                return JobCondition.InterruptForced;
            }
            return JobCondition.Ongoing;
        });
    }
}

//利用可能なthingに車上マップ上のthingを含める
[HarmonyPatch(typeof(ItemAvailability), nameof(ItemAvailability.ThingsAvailableAnywhere))]
[PatchLevel(Level.Sensitive)]
public static class Patch_ItemAvailability_ThingsAvailableAnywhere
{
    private static readonly List<Thing> tmpList = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var pos = code.FindIndex(c => c.opcode == OpCodes.Stloc_2);
        code.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(ItemAvailability), "map"),
            CodeInstruction.LoadArgument(1),
            CodeInstruction.Call(typeof(Patch_ItemAvailability_ThingsAvailableAnywhere), nameof(AddThingList))
        ]);
        return code;
    }

    public static List<Thing> AddThingList(List<Thing> list, Map map, ThingDef need)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        tmpList.AddRange(map.BaseMapAndVehicleMaps().Except(map).SelectMany(m => m.listerThings.ThingsOfDef(need)));
        return tmpList;
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
public static class Patch_GenClosest_ClosestThingReachable
{
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    [PatchLevel(Level.Mandatory)]
    [MethodImpl(MethodImplOptions.NoInlining)] //リバースパッチはインライン化させないほうがいい。これ豆な
    public static Thing ClosestThingReachableOriginal(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode,
        TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator,
        IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax,
        bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions,
        bool lookInHaulSources) => throw new NotImplementedException();

    [PatchLevel(Level.Safe)]
    public static void Prefix(ref Map map, TraverseParms traverseParams)
    {
        var map2 = traverseParams.pawn?.DepartMap;
        if (map2 != null) map = map2;
    }

    [PatchLevel(Level.Safe)]
    public static void Postfix(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources, ref Thing __result)
    {
        __result ??= GenClosestCrossMap.ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Regionwise_ReachablePrioritized))]
[PatchLevel(Level.Safe)]
public static class Patch_GenClosest_ClosestThing_Regionwise_ReachablePrioritized
{
    public static void Prefix(ref Map map, TraverseParms traverseParams)
    {
        var map2 = traverseParams.pawn?.DepartMap;
        if (map2 != null) map = map2;
    }

    public static void Postfix(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, int minRegions, int maxRegions, bool lookInHaulSources, ref Thing __result)
    {
        __result ??= GenClosestCrossMap.ClosestThing_Regionwise_ReachablePrioritized(root, map, thingReq, peMode, traverseParams, maxDistance, validator, priorityGetter, minRegions, maxRegions, lookInHaulSources);
    }
}

[HarmonyPatch(typeof(RegionProcessorClosestThingReachable), "ProcessThing")]
[HarmonyPriority(Priority.High)]
[PatchLevel(Level.Mandatory)]
public static class Patch_RegionProcessorClosestThingReachable_ProcessThing
{
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    public static void ProcessThing(RegionProcessorClosestThingReachable instance, Region reg, Thing t)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_PositionHeld, CachedMethodInfo.m_PositionHeldOnBaseMap);
        }
    }
}

[HarmonyPatch(typeof(RegionProcessorClosestThingReachable), "RegionProcessor")]
[HarmonyPriority(Priority.Normal)]
[PatchLevel(Level.Mandatory)]
public static class Patch_RegionProcessorClosestThingReachable_RegionProcessor
{
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    public static bool RegionProcessorBaseMapCoord(this RegionProcessorClosestThingReachable instance, Region reg)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ProcessThingOrig = AccessTools.Method(typeof(RegionProcessorClosestThingReachable), "ProcessThing");
            var m_ProcessThing = AccessTools.Method(typeof(Patch_RegionProcessorClosestThingReachable_ProcessThing), nameof(Patch_RegionProcessorClosestThingReachable_ProcessThing.ProcessThing));
            return instructions.MethodReplacer(m_ProcessThingOrig, m_ProcessThing);
        }
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.Reserve))]
public static class Patch_ReservationManager_Reserve
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(ref ReservationManager __instance, Map ___map, Pawn claimant, Job job, LocalTargetInfo target)
    {
        if (ShouldReplace(___map, claimant, target, false, out var map, job))
        {
            __instance = map.reservationManager;
        }
    }

    public static bool ShouldReplace(Map ___map, Pawn claimant, LocalTargetInfo target, bool allowSameMap, out Map map, Job job = null)
    {
        //CTDに繋がる可能性があるので無限ループが起きないよう注意
        map = target.Thing?.MapHeld;
        if (map is null && !TargetMapManager.HasTargetMap(claimant, out map) && (job is null || (LocalTargetInfo)job.globalTarget != target || (map = job.globalTarget.Map) is null))
        {
            return false;
        }
        return allowSameMap || ___map != map;
    }

    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
        codes.Repeat(c =>
        {
            c.InstructionAt(-1).opcode = OpCodes.Ldarg_0;
            c.Opcode = OpCodes.Ldfld;
            c.Operand = AccessTools.Field(typeof(ReservationManager), "map");
        });
        return codes.Instructions();
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.ReservedBy), typeof(LocalTargetInfo), typeof(Pawn), typeof(Job))]
[PatchLevel(Level.Safe)]
public static class Patch_ReservationManager_ReservedBy
{
    public static void Prefix(ref ReservationManager __instance, Map ___map, Pawn claimant, LocalTargetInfo target, Job job)
    {
        if (Patch_ReservationManager_Reserve.ShouldReplace(___map, claimant, target, false, out var map, job))
        {
            __instance = map.reservationManager;
        }
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserve))]
[PatchLevel(Level.Safe)]
public static class Patch_ReservationManager_CanReserve
{
    public static bool Prefix(Map ___map, Pawn claimant, LocalTargetInfo target, int maxPawns, int stackCount, ReservationLayerDef layer, bool ignoreOtherReservations, ref bool __result)
    {
        if (Patch_ReservationManager_Reserve.ShouldReplace(___map, claimant, target, true, out var map))
        {
            __result = claimant.CanReserve(target, maxPawns, stackCount, layer, ignoreOtherReservations, map);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserveStack))]
[PatchLevel(Level.Sensitive)]
public static class Patch_ReservationManager_CanReserveStack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
        codes[pos] = new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing);

        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
        codes.Insert(pos2, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map));
        return codes;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.TryGetReserver))]
[PatchLevel(Level.Safe)]
public static class Patch_ReservationManager_TryGetReserver
{
    public static bool Prefix(ref ReservationManager __instance, Map ___map, LocalTargetInfo target)
    {
        Map thingMap;
        if ((thingMap = target.Thing?.MapHeld) != null && ___map != thingMap)
        {
            __instance = thingMap.reservationManager;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.FirstRespectedReserver))]
[PatchLevel(Level.Safe)]
public static class Patch_ReservationManager_FirstRespectedReserver
{
    public static void Prefix(ref ReservationManager __instance, Map ___map, LocalTargetInfo target, Pawn claimant)
    {
        if (Patch_ReservationManager_Reserve.ShouldReplace(___map, claimant, target, false, out var map))
        {
            __instance = map.reservationManager;
        }
    }
}

//FoodSourceの一覧に車上マップの物を含める
[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
[PatchLevel(Level.Sensitive)]
public static class Patch_FoodUtility_BestFoodSourceOnMap
{
    private static readonly List<Thing> searchSet = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var m_ThingsMatching = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsMatching));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsMatching)) + 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadLocal(1),
            CodeInstruction.Call(typeof(Patch_FoodUtility_BestFoodSourceOnMap), nameof(AddSearchSet))
        ]);
        return codes;
    }

    private static List<Thing> AddSearchSet(List<Thing> list, Pawn getter, ThingRequest req)
    {
        searchSet.Clear();
        searchSet.AddRange(list);
        var maps = getter.Map.BaseMapAndVehicleMaps().Except(getter.Map);
        foreach (var map in maps)
        {
            searchSet.AddRange(map.listerThings.ThingsMatching(req));
        }
        return searchSet;
    }
}

[HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
[PatchLevel(Level.Sensitive)]
public static class Patch_RestUtility_CanUseBedNow
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        //!building_Bed.Position.IsInPrisonCell(building_Bed.Map)があるので置き換えるのは最初のMapのみ
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
            .Set(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_MapHeld))
            .Set(OpCodes.Call, CachedMethodInfo.m_MapHeldBaseMapOrCaravan)
            .Instructions();
    }
}

[HarmonyPatch(typeof(ToilFailConditions), nameof(ToilFailConditions.DespawnedOrNull))]
[PatchLevel(Level.Cautious)]
public static class Patch_ToilFailConditions_DespawnedOrNull
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMapOrCaravan_Thing);
    }
}

[HarmonyPatch(typeof(ToilFailConditions), nameof(ToilFailConditions.SelfAndParentsDespawnedOrNull))]
[PatchLevel(Level.Cautious)]
public static class Patch_ToilFailConditions_SelfAndParentsDespawnedOrNull
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMapOrCaravan)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMapOrCaravan_Thing);
    }
}

[HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), typeof(Thing), typeof(Pawn))]
[PatchLevel(Level.Safe)]
public static class Patch_ForbidUtility_IsForbidden
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IsForbidden))
            .InsertAndAdvance(CodeInstruction.LoadArgument(0))
            .SetOperandAndAdvance(CachedMethodInfo.m_CrossMapIsForbidden)
            .InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.InAllowedArea))]
[PatchLevel(Level.Safe)]
public static class Patch_ForbidUtility_InAllowedArea
{
    public static bool Prefix(IntVec3 c, Pawn forPawn, ref Map __state)
    {
        if (TargetMapManager.HasTargetMap(forPawn, out var map) && map != forPawn.Map)
        {
            __state = forPawn.Map;
            forPawn.VirtualMapTransfer(map);
        }
        return c.InBounds(forPawn.MapHeld);
    }

    public static void Finalizer(IntVec3 c, Pawn forPawn, Map __state, ref bool __result)
    {
        if (__state is not null)
            forPawn.VirtualMapTransfer(__state);
        if (forPawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned && __result)
        {
            using var _ = new VirtualTeleporter(forPawn, vehicle.Map);
            __result = c.ToBaseMapCoord(vehicle).InAllowedArea(forPawn);
        }
    }
}

[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.DutyLocation))]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnUtility_DutyLocation
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_ToilFailConditions_FailOnSomeonePhysicallyInteracting
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(ToilFailConditions), t =>
        {
            var type = t.IsGenericTypeDefinition ? t.MakeGenericType(typeof(Toil)) : t;
            return type.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<FailOnSomeonePhysicallyInteracting>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        return codes.Select((c, i) =>
        {
            if (c.opcode != OpCodes.Callvirt || !c.OperandIs(CachedMethodInfo.g_Thing_Map)) return c;
            codes[i - 1].opcode = OpCodes.Ldloc_1;
            c.operand = CachedMethodInfo.g_Thing_MapHeld;
            return c;
        });
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_ToilFailConditions_FailOnBurningImmobile
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.InnerTypes(typeof(ToilFailConditions)).SelectMany(t =>
        {
            var type = t.IsGenericTypeDefinition ? t.MakeGenericType(typeof(Toil)) : t;
            return type.GetDeclaredMethods();
        }).First(m => m.Name.Contains("<FailOnBurningImmobile>"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var ind = original.GetMethodBody()!.LocalVariables.FirstIndexOf(l => l.LocalType == typeof(LocalTargetInfo));
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
            .Set(OpCodes.Call, AccessTools.Method(typeof(Patch_ToilFailConditions_FailOnBurningImmobile), nameof(ThingMapOrTargetMapOrPawnMap)))
            .Insert(CodeInstruction.LoadLocal(ind));
        return codes.Instructions();
    }

    private static Map ThingMapOrTargetMapOrPawnMap(Pawn pawn, LocalTargetInfo target)
    {
        var map = target.Thing?.MapHeld ?? TargetMapManager.TargetMapOrPawnMap(pawn);
        return !target.Cell.InBounds(map) ? map.BaseMap() : map;
    }
}

//JobDriver_GotoDestMapはnextJobを使ってReservationを行っているので、それを使って解放しなければならない
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ClearReservationsForJob))]
[PatchLevel(Level.Mandatory)]
public static class Patch_Pawn_ClearReservationsForJob
{
    public static void Prefix(ref Job job, Pawn __instance)
    {
        if (job?.def != null && job.GetCachedDriver(__instance) is JobDriver_GotoDestMap gotoDestMap)
        {
            job = gotoDestMap.nextJob;
        }
    }
}

[HarmonyPatch(typeof(TransporterUtility), nameof(TransporterUtility.GetTransportersInGroup))]
[PatchLevel(Level.Safe)]
public static class Patch_TransporterUtility_GetTransportersInGroup
{
    public static void Postfix(int transportersGroup, Map map, List<CompTransporter> outTransporters)
    {
        if (transportersGroup < 0)
        {
            return;
        }

        outTransporters.AddRange(VehiclePawnWithMapCache.AllVehiclesOn(map.BaseMap())
            .SelectMany(vehicle => vehicle.ContainerComps)
            .Where(compTransporter => compTransporter.groupID == transportersGroup));
    }
}

[HarmonyPatch(typeof(ThingOwner), "NotifyAdded")]
[PatchLevel(Level.Safe)]
public static class Patch_ThingOwner_NotifyAdded
{
    public static void Postfix(Thing item, IThingHolder ___owner)
    {
        if (___owner is Pawn_InventoryTracker { pawn: VehiclePawnWithMap vehicle })
        {
            foreach (var container in vehicle.VehicleMap.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompBuildableContainer>()))
            {
                var comp = container.TryGetComp<CompBuildableContainer>();
                comp.Notify_ThingAdded(item);
            }
        }
    }
}

[HarmonyPatch(typeof(ThingOwner), "NotifyAddedAndMergedWith")]
[PatchLevel(Level.Safe)]
public static class Patch_ThingOwner_NotifyAddedAndMergedWith
{
    public static void Postfix(Thing item, IThingHolder ___owner, int mergedCount)
    {
        if (___owner is Pawn_InventoryTracker { pawn: VehiclePawnWithMap vehicle })
        {
            foreach (var container in vehicle.VehicleMap.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompBuildableContainer>()))
            {
                var comp = container.TryGetComp<CompBuildableContainer>();
                comp.Notify_ThingAddedAndMergedWith(item, mergedCount);
            }
        }
    }
}

[HarmonyPatch(typeof(JobDriver_Ingest), nameof(JobDriver_Ingest.ModifyCarriedThingDrawPosWorker))]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_Ingest_ModifyCarriedThingDrawPosWorker
{
    public static void Postfix(ref Vector3 drawPos, Pawn pawn, bool __result)
    {
        if (__result && pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            drawPos = drawPos.ToBaseMapCoord(vehicle).WithY(drawPos.y);
        }
    }
}

//FoodDeliverはtargetCのセルに向かってStartPathしてるのでtargetB（囚人）とのマップの違いをチェックしてそのマップに行く必要がある
[HarmonyPatch(typeof(JobDriver_FoodDeliver), "MakeNewToils")]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_FoodDeliver_MakeNewToils
{
    public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values, Job ___job)
    {
        var found = false;
        foreach (var toil in values)
        {
            if (toil.debugName == "MakeNewToils" && !found)
            {
                found = true;
                toil.AddPreInitAction(() =>
                {
                    if (___job.targetB.HasThing && toil.actor.Map != ___job.targetB.Thing.MapHeld &&
                        toil.actor.CanReach(___job.targetB, PathEndMode.Touch, Danger.Deadly, false, false,
                            TraverseMode.ByPawn, ___job.targetB.Thing.MapHeld, out var exitSpot, out var enterSpot,
                            out var spotsQueue))
                    {
                        JobAcrossMapsUtility.StartGotoDestMapJob(toil.actor, exitSpot, enterSpot, spotsQueue);
                    }
                });
            }
            yield return toil;
        }
    }
}

//billGiverRootCell.GetRegion(pawn.Map, RegionType.Set_Passable); -> billGiverRootCell.GetRegion(billGiver.Map, RegionType.Set_Passable);
[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_DoBill_TryFindBestIngredientsHelper
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.Calls(CachedMethodInfo.g_Thing_Map));
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Pop),
            CodeInstruction.LoadArgument(4)
        ]);
        return codes.MethodReplacer(CachedMethodInfo.m_BreadthFirstTraverse, CachedMethodInfo.m_BreadthFirstTraverseAcrossMaps);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_DoBill_TryFindBestIngredientsHelper_Predicate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(WorkGiver_DoBill),
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name == "<TryFindBestIngredientsHelper>b__0"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorkGiver_ConstructDeliverResources_ResourceDeliverJobFor_Delegate
{
    private static MethodBase TargetMethod()
    {
        Type[] fields = [typeof(Thing)];
        Type[] args = [typeof(Thing)];
        return AccessTools.FindIncludingInnerTypes(typeof(WorkGiver_ConstructDeliverResources), t =>
        {
            if (!t.GetDeclaredFields().Select(f => f.FieldType).SequenceEqual(fields)) return null;
            return t.GetDeclaredMethods().FirstOrDefault(m =>
            {
                return m.GetParameters().Select(p => p.ParameterType).SequenceEqual(args) &&
                       m.Name.Contains("<ResourceDeliverJobFor>");
            });
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_ToilFailConditions_FailOnForbidden_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(ToilFailConditions), t =>
        {
            var type = t.IsGenericTypeDefinition ? t.MakeGenericType(typeof(Toil)) : t;
            return type.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<FailOnForbidden>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IsForbidden))
            .InsertAndAdvance(CodeInstruction.LoadLocal(2))
            .SetOperandAndAdvance(CachedMethodInfo.m_CrossMapIsForbidden)
            .InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(WanderUtility), nameof(WanderUtility.GetColonyWanderRoot))]
[PatchLevel(Level.Cautious)]
public static class Patch_WanderUtility_GetColonyWanderRoot
{
    public static List<Pawn> FreeColonistsSpawned(MapPawns instance) => Patch_MapPawns_FreeHumanlikesSpawnedOfFaction.FreeHumanlikesSpawnedOfFaction(instance, Faction.OfPlayer);

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(
            AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.FreeColonistsSpawned)),
            AccessTools.Method(typeof(Patch_WanderUtility_GetColonyWanderRoot), nameof(FreeColonistsSpawned)));
    }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.ClearCache))]
[PatchLevel(Level.Safe)]
public static class Patch_Reachability_ClearCache
{
    public static void Postfix(Map ___map)
    {
        CrossMapReachabilityCache.ClearCacheFor(___map);
    }
}