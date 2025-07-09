using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
public static class Patch_Pawn_JobTracker_StartJob
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_MakeDriver = AccessTools.Method(typeof(Job), nameof(Job.MakeDriver));
        var m_GetCachedDriver = AccessTools.Method(typeof(Job), nameof(Job.GetCachedDriver));
        return instructions.MethodReplacer(m_MakeDriver, m_GetCachedDriver);
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), "TryFindAndStartJob")]
public static class Patch_Pawn_JobTracker_TryFindAndStartJob
{
    public static void Prefix(Pawn ___pawn)
    {
        TargetMapManager.TargetMap.Remove(___pawn);
    }
}

[HarmonyAfter("SmarterConstruction")]
[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
public static class Patch_JobGiver_Work_TryIssueJobPackage
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        //scanner変数をローカルに保存しておく
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Isinst && c.operand.Equals(typeof(WorkGiver_Scanner))) + 1;
        var scanner = generator.DeclareLocal(typeof(WorkGiver_Scanner));
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Stloc_S, scanner)
        ]);

        //サーチセットに複数マップのthingリストを足す
        pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 16);

        var addedCodes = new[]
        {
            CodeInstruction.LoadArgument(1),
            new CodeInstruction(OpCodes.Ldloc_S, scanner),
            CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(AddSearchSet))
        };
        codes.InsertRange(pos, addedCodes);

        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 18);
        codes.InsertRange(pos2, addedCodes);

        var pos3 = codes.FindIndex(pos2, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 20) + 1;
        codes.InsertRange(pos3,
        [
            new CodeInstruction(OpCodes.Ldloc_S, scanner),
            CodeInstruction.LoadLocal(0, true),
            CodeInstruction.LoadLocal(19, true),
            CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(ScanCellsAcrossMaps))
        ]);

        var m_JobOnCell = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnCell));
        var pos4 = codes.FindIndex(pos3, c => c.Calls(m_JobOnCell));
        codes[pos4].opcode = OpCodes.Call;
        codes[pos4].operand = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(JobOnCellMap));

        var g_TargetInfo_Cell = AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Cell));
        var pos5 = codes.FindLastIndex(pos4, c => c.Calls(g_TargetInfo_Cell));
        codes.RemoveAt(pos5);

        //GenClosestの各メソッドを自作のものに置き換える
        //PotentialWorkThingsGlobalの各マップの結果を合計
        var m_GenClosest_ClosestThing_Global = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
        var m_GenClosestOnVehicle_ClosestThing_Global = AccessTools.Method(typeof(GenClosestCrossMap), nameof(GenClosestCrossMap.ClosestThing_Global),
            [typeof(IntVec3), typeof(IEnumerable<>), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>), typeof(bool)]);
        var m_GenClosest_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
        var m_GenClosestOnVehicle_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosestCrossMap), nameof(GenClosestCrossMap.ClosestThing_Global_Reachable),
            [typeof(IntVec3), typeof(Map), typeof(IEnumerable<Thing>), typeof(PathEndMode), typeof(TraverseParms), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>), typeof(bool)]);
        var m_Scanner_PotentialWorkThingsGlobal = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal));
        var m_PotentialWorkThingsGlobalAll = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(PotentialWorkThingsGlobalAll));
        var m_Scanner_JobOnThing = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnThing));
        var m_JobOnThingMap = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(JobOnThingMap));
        return codes.MethodReplacer(m_GenClosest_ClosestThing_Global, m_GenClosestOnVehicle_ClosestThing_Global)
            .MethodReplacer(m_GenClosest_ClosestThing_Global_Reachable, m_GenClosestOnVehicle_ClosestThing_Global_Reachable)
            .MethodReplacer(m_Scanner_PotentialWorkThingsGlobal, m_PotentialWorkThingsGlobalAll)
            .MethodReplacer(m_Scanner_JobOnThing, m_JobOnThingMap);
    }

    private static IEnumerable<Thing> AddSearchSet(List<Thing> list, Pawn pawn, WorkGiver_Scanner scanner)
    {
        if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, null, scanner))
        {
            return list;
        }
        var maps = pawn.Map.BaseMapAndVehicleMaps().Except(pawn.Map);
        if (maps.Any())
        {
            return maps.SelectMany(m => m.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest)).ConcatIfNotNull(list).Distinct();
        }
        return list;
    }

    private static IEnumerable<Thing> PotentialWorkThingsGlobalAll(WorkGiver_Scanner scanner, Pawn pawn)
    {
        if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, null, scanner))
        {
            return scanner.PotentialWorkThingsGlobal(pawn);
        }
        CrossMapReachabilityUtility.tmpDepartMap = pawn.Map;
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
            pawn.VirtualMapTransfer(CrossMapReachabilityUtility.tmpDepartMap, pos);
            CrossMapReachabilityUtility.tmpDepartMap = null;
        }
    }

    private static Job JobOnThingMap(WorkGiver_Scanner scanner, Pawn pawn, Thing t, bool forced)
    {
        var thingMap = t.MapHeld;
        if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, thingMap, scanner))
        {
            return scanner.JobOnThing(pawn, t, forced);
        }

        var map = pawn.Map;
        if (!scanner.AllowUnreachable)
        {
            if (pawn.CanReach(t, scanner.PathEndMode, scanner.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, thingMap, out _, out _))
            {
                var pos = pawn.Position;
                var dest = t.PositionHeld;
                pawn.VirtualMapTransfer(thingMap, dest);
                Job job;
                try
                {
                    job = scanner.JobOnThing(pawn, t, forced);
                }
                finally
                {
                    pawn.VirtualMapTransfer(map, pos);
                }
                return job;
            }
            return null;
        }
        var cell = pawn.Position;
        var cell2 = CellRect.WholeMap(thingMap).RandomCell;
        pawn.VirtualMapTransfer(thingMap, cell2);
        try
        {
            return scanner.JobOnThing(pawn, t, forced);
        }
        finally
        {
            pawn.VirtualMapTransfer(map, cell);
        }
    }

    private static Job JobOnCellMap(WorkGiver_Scanner scanner, Pawn pawn, in TargetInfo target, bool forced)
    {
        var map = pawn.Map;
        var targetMap = target.Map;
        if (map == targetMap || (scanner is IWorkGiverAcrossMaps workGiverAcrossMaps && !workGiverAcrossMaps.NeedVirtualMapTransfer))
        {
            return scanner.JobOnCell(pawn, target.Cell, forced);
        }

        if (pawn.CanReach(target.Cell, scanner.PathEndMode, scanner.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, targetMap, out var exitSpot, out var enterSpot))
        {
            var pos = pawn.Position;
            var dest = target.Cell;
            pawn.VirtualMapTransfer(targetMap, dest);
            try
            {
                return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, scanner.JobOnCell(pawn, dest, forced));
            }
            finally
            {
                pawn.VirtualMapTransfer(map, pos);
            }
        }
        return null;
    }

    private static void ScanCellsAcrossMaps(WorkGiver_Scanner scanner, ref InnerClass innerClass, ref InnerStruct innerStruct)
    {
        var pawn = innerClass.pawn;
        var basePos = pawn.PositionOnBaseMap();
        var map = CrossMapReachabilityUtility.tmpDepartMap = pawn.Map;
        var maps = map.BaseMapAndVehicleMaps().Except(map);
        try
        {
            foreach (var map2 in maps)
            {
                pawn.VirtualMapTransfer(map2);
                var positionOnMap = map2.IsVehicleMapOf(out var vehicle) ? basePos.ToVehicleMapCoord(vehicle) : basePos;
                IEnumerable<IntVec3> enumerable2 = scanner.PotentialWorkCellsGlobal(pawn);
                foreach (IntVec3 c in enumerable2)
                {
                    if (CrossMapReachabilityUtility.CanReach(map, innerStruct.pawnPosition, c, scanner.PathEndMode, TraverseParms.For(pawn, innerStruct.maxPathDanger), map2, out _, out _))
                    {
                        pawn.SetPositionDirect(c);
                    }
                    bool flag2 = false;
                    float num4 = (c - positionOnMap).LengthHorizontalSquared;
                    float num5 = 0f;
                    try
                    {
                        Patch_ForbidUtility_IsForbidden.Map = map2;
                        if (innerStruct.prioritized)
                        {
                            if (!c.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, c))
                            {
                                num5 = scanner.GetPriority(pawn, c);
                                if (num5 > innerStruct.bestPriority || (num5 == innerStruct.bestPriority && num4 < innerStruct.closestDistSquared))
                                {
                                    flag2 = true;
                                }
                            }
                        }
                        else if (num4 < innerStruct.closestDistSquared && !c.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, c))
                        {
                            flag2 = true;
                        }
                    }
                    finally
                    {
                        Patch_ForbidUtility_IsForbidden.Map = null;
                    }

                    if (flag2)
                    {
                        innerClass.bestTargetOfLastPriority = new TargetInfo(c, map2);
                        innerClass.scannerWhoProvidedTarget = scanner;
                        innerStruct.closestDistSquared = num4;
                        innerStruct.bestPriority = num5;
                    }
                }
            }
        }
        finally
        {
            pawn.VirtualMapTransfer(CrossMapReachabilityUtility.tmpDepartMap, innerStruct.pawnPosition);
            CrossMapReachabilityUtility.tmpDepartMap = null;
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
public static class Patch_JobGiver_Work_PawnCanUseWorkGiver
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_WorkGiver_ShouldSkip = AccessTools.Method(typeof(WorkGiver), nameof(WorkGiver.ShouldSkip));
        var m_ShouldSkipAll = AccessTools.Method(typeof(Patch_JobGiver_Work_PawnCanUseWorkGiver), nameof(ShouldSkipAll));
        return instructions.MethodReplacer(m_WorkGiver_ShouldSkip, m_ShouldSkipAll);
    }

    public static bool ShouldSkipAll(this WorkGiver workGiver, Pawn pawn, bool forced)
    {
        var map = pawn.Map;
        try
        {
            return pawn.Map.BaseMapAndVehicleMaps().All(m =>
            {
                pawn.VirtualMapTransfer(m);
                return workGiver.ShouldSkip(pawn, forced);
            });
        }
        finally
        {
            pawn.VirtualMapTransfer(map);
        }
    }
}

[HarmonyPatch]
public static class Patch_JobGiver_Work_Validator
{
    public static MethodInfo TargetMethod()
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
    private static bool HasJobOnThingMap(WorkGiver_Scanner scanner, Pawn pawn, Thing t, bool forced)
    {
        var thingMap = t.MapHeld;
        if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, thingMap, scanner))
        {
            return scanner.HasJobOnThing(pawn, t, forced);
        }

        var map = pawn.Map;
        if (!scanner.AllowUnreachable)
        {
            if (pawn.CanReach(t, scanner.PathEndMode, scanner.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, thingMap, out _, out _))
            {
                var pos = pawn.Position;
                var dest = t.PositionHeld;
                pawn.VirtualMapTransfer(thingMap, dest);
                try
                {
                    return scanner.HasJobOnThing(pawn, t, forced);
                }
                finally
                {
                    pawn.VirtualMapTransfer(map, pos);
                }
            }
            return false;
        }
        var cell = pawn.Position;
        var cell2 = CellRect.WholeMap(thingMap).RandomCell;
        pawn.VirtualMapTransfer(thingMap, cell2);
        try
        {
            return scanner.HasJobOnThing(pawn, t, forced);
        }
        finally
        {
            pawn.VirtualMapTransfer(map, cell);
        }
    }

    public static Map tmpMap;

    public static IntVec3 tmpCell = IntVec3.Invalid;
}

[HarmonyPatch]
public static class Patch_JobGiver_Work_GiverTryGiveJobPrioritized
{
    public static MethodInfo TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(JobGiver_Work), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<GiverTryGiveJobPrioritized>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_JobGiver_Work_Validator.Transpiler(instructions);
}

[HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
public static class Patch_Pawn_PathFollower_StartPath
{
    public static bool Prefix(LocalTargetInfo dest, PathEndMode peMode, Pawn ___pawn)
    {
        if (___pawn.CurJob == null) return true;

        Map destMap = dest.Thing?.MapHeld;
        if (destMap == null)
        {
            return true;
        }
        if (___pawn.Map != destMap && ___pawn.CanReach(dest, peMode, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap, out var exitSpot, out var enterSpot))
        {
            JobAcrossMapsUtility.StartGotoDestMapJob(___pawn, exitSpot, enterSpot);
            return false;
        }
        return true;
    }
}

//targetにThingが入ってるのにGotoCellを使ってるようなケースでは先にマップが違うかどうかチェックする
[HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), typeof(IntVec3), typeof(PathEndMode))]
public static class Patch_Toils_Goto_GotoCell
{
    [HarmonyPatch([typeof(IntVec3), typeof(PathEndMode)])]
    [HarmonyPostfix]
    public static void Postfix1(IntVec3 cell, PathEndMode peMode, Toil __result)
    {
        __result.AddPreInitAction(() =>
        {
            var actor = __result.actor;
            var curJob = actor.CurJob;
            var allTargets = new[] { curJob.targetA, curJob.targetB, curJob.targetC }.ConcatIfNotNull(curJob.targetQueueA).ConcatIfNotNull(curJob.targetQueueB);
            var target = allTargets.FirstOrFallback(t => t.HasThing && (t.Cell == cell || (t.Thing.Spawned && t.Thing.InteractionCell == cell)), LocalTargetInfo.Invalid);
            if (target.IsValid && actor.Map != target.Thing.MapHeld && actor.CanReach(target, peMode, Danger.Deadly, false, false, TraverseMode.ByPawn, target.Thing.MapHeld, out var exitSpot, out var enterSpot))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot);
            }
        });
    }

    //PUAHのJobDriver_HaulToInventoryなど
    [HarmonyPatch([typeof(TargetIndex), typeof(PathEndMode)])]
    [HarmonyPostfix]
    public static void Postfix2(TargetIndex ind, PathEndMode peMode, Toil __result)
    {
        __result.AddPreInitAction(() =>
        {
            var actor = __result.actor;
            if (!TargetMapManager.HasTargetMap(actor, out var map))
            {
                return;
            }
            var target = actor.jobs.curJob.GetTarget(ind);
            if (actor.CanReach(target, peMode, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot);
                TargetMapManager.TargetMap.Remove(actor);
            }
        });
    }
}

[HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoBuild))]
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
            if (thingMap != null && actor.Map != thingMap && actor.CanReach(target, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, thingMap, out var exitSpot, out var enterSpot))
            {
                JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot);
            }
        });
    }
}

//GotoCellと同じやり方でSittableOrSpotのチェック
[HarmonyPatch(typeof(ReservationUtility), nameof(ReservationUtility.ReserveSittableOrSpot))]
public static class Patch_ReservationUtility_ReserveSittableOrSpot
{
    public static void Prefix(Pawn pawn, IntVec3 exactSittingPos, Job job, ref Map __state)
    {
        var allTargets = new[] { job.targetA, job.targetB, job.targetC }.ConcatIfNotNull(job.targetQueueA).ConcatIfNotNull(job.targetQueueB);
        var target = allTargets.FirstOrFallback(t => t.HasThing && (t.Cell == exactSittingPos || (t.Thing.Spawned && t.Thing.InteractionCell == exactSittingPos)), LocalTargetInfo.Invalid);
        if (target.IsValid && pawn.Map != target.Thing.MapHeld)
        {
            __state = pawn.Map;
            pawn.VirtualMapTransfer(target.Thing.MapHeld);
        }
    }

    public static void Finalizer(Pawn pawn, Map __state)
    {
        if (__state != null)
        {
            pawn.VirtualMapTransfer(__state);
        }
    }
}

//GotoCellと同じやり方でSittableOrSpotのチェック
[HarmonyPatch(typeof(ReservationUtility), nameof(ReservationUtility.CanReserveSittableOrSpot), [typeof(Pawn), typeof(IntVec3), typeof(Thing), typeof(bool)])]
public static class Patch_ReservationUtility_CanReserveSittableOrSpot
{
    public static bool Prefix(Pawn pawn, IntVec3 exactSittingPos, Thing ignoreThing, ref Map __state, ref bool __result)
    {
        Patch_ForbidUtility_IsForbidden.Map = ignoreThing?.Map;

        if (!exactSittingPos.InBounds(pawn.Map))
        {
            var maps = pawn.Map.BaseMapAndVehicleMaps().Except(pawn.Map);
            Map map;
            if ((map = maps.FirstOrDefault(m => exactSittingPos.IsBuildingInteractionCell(m))) != null)
            {
                __state = pawn.Map;
                pawn.VirtualMapTransfer(map);
                return true;
            }
            __result = false;
            return false;
        }
        return true;
    }

    public static void Finalizer(Pawn pawn, Map __state)
    {
        Patch_ForbidUtility_IsForbidden.Map = null;

        if (__state != null)
        {
            pawn.VirtualMapTransfer(__state);
        }
    }
}

//ChewSpotのThingが見つかった場合にGotoTargetMapを挟む
[HarmonyPatch]
public static class Patch_Toils_Ingest_CarryIngestibleToChewSpot_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(Toils_Ingest), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<CarryIngestibleToChewSpot>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Stloc_2)
            {
                var label = generator.DefineLabel();
                yield return CodeInstruction.LoadLocal(2);
                yield return CodeInstruction.LoadLocal(0);
                yield return CodeInstruction.Call(typeof(Patch_Toils_Ingest_CarryIngestibleToChewSpot_Delegate), nameof(StartGotoDestMapJob));
                yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                yield return new CodeInstruction(OpCodes.Ret);
                yield return new CodeInstruction(OpCodes.Nop).WithLabels(label);
            }
        }
    }

    private static bool StartGotoDestMapJob(Thing thing, Pawn pawn)
    {
        if (thing == null)
        {
            return false;
        }
        var parent = thing.SpawnedParentOrMe;
        if (parent != null && pawn.Map != parent.Map && pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, parent.Map, out var exitSpot, out var enterSpot))
        {
            JobAcrossMapsUtility.StartGotoDestMapJob(pawn, exitSpot, enterSpot);
            return true;
        }
        return false;
    }
}


[HarmonyPatch(typeof(Toils_Bed), nameof(Toils_Bed.GotoBed))]
public static class Patch_Toils_Bed_GotoBed
{
    public static void Postfix(TargetIndex bedIndex, Toil __result)
    {
        __result.AddPreInitAction(() =>
        {
            var pawn = __result.actor;
            var bed = pawn.CurJob.GetTarget(bedIndex).Thing;
            if (pawn.Map != bed.Map && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, bed.Map, out var exitSpot, out var enterSpot))
            {
                var nextJob = pawn.CurJob.Clone();
                pawn.jobs.curDriver.globalFinishActions.Clear();
                pawn.jobs.StartJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, nextJob), JobCondition.InterruptForced, keepCarryingThingOverride: true);
            }
        });
    }
}

//利用可能なthingに車上マップ上のthingを含める
[HarmonyPatch(typeof(ItemAvailability), nameof(ItemAvailability.ThingsAvailableAnywhere))]
public static class Patch_ItemAvailability_ThingsAvailableAnywhere
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var pos = code.FindIndex(c => c.opcode == OpCodes.Stloc_2);
        code.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(ItemAvailability), "map"),
            CodeInstruction.LoadArgument(1),
            CodeInstruction.Call(typeof(Patch_ItemAvailability_ThingsAvailableAnywhere), nameof(Patch_ItemAvailability_ThingsAvailableAnywhere.AddThingList))
        ]);
        return code;
    }

    public static List<Thing> AddThingList(List<Thing> list, Map map, ThingDef need)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(map))
        {
            tmpList.AddRange(vehicle.VehicleMap.listerThings.ThingsOfDef(need));
        }
        return tmpList;
    }

    private static readonly List<Thing> tmpList = [];
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
public static class Patch_GenClosest_ClosestThingReachable
{
    [HarmonyReversePatch(HarmonyReversePatchType.Original)]
    public static Thing ClosestThingReachableOriginal(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources) => throw new NotImplementedException();

    public static bool Prefix(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources, ref Thing __result)
    {
        var maps = map.BaseMapAndVehicleMaps().Except(map);
        if (traverseParams.pawn != null && maps.Any())
        {
            __result = GenClosestCrossMap.ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThing_Regionwise_ReachablePrioritized))]
public static class Patch_GenClosest_ClosestThing_Regionwise_ReachablePrioritized
{
    public static bool Prefix(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, int minRegions, int maxRegions, bool lookInHaulSources, ref Thing __result)
    {
        var maps = map.BaseMapAndVehicleMaps().Except(map);
        if (traverseParams.pawn != null && maps.Any())
        {
            __result = GenClosestCrossMap.ClosestThing_Regionwise_ReachablePrioritized(root, map, thingReq, peMode, traverseParams, maxDistance, validator, priorityGetter, minRegions, maxRegions, lookInHaulSources);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(RegionProcessorClosestThingReachable), "ProcessThing")]
public static class Patch_RegionProcessorClosestThingReachable_ProcessThing
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_PositionHeld, CachedMethodInfo.m_PositionHeldOnBaseMap);
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.Reserve))]
public static class Patch_ReservationManager_Reserve
{
    public static bool Prefix(Map ___map, Pawn claimant, Job job, LocalTargetInfo target, int maxPawns, int stackCount, ReservationLayerDef layer, bool errorOnFailed, bool ignoreOtherReservations, bool canReserversStartJobs, ref bool __result)
    {
        if (ShouldReplace(___map, claimant, target, false, out var map))
        {
            __result = map.reservationManager.Reserve(claimant, job, target, maxPawns, stackCount, layer, errorOnFailed, ignoreOtherReservations, canReserversStartJobs);
            return false;
        }
        return true;
    }

    public static bool ShouldReplace(Map ___map, Pawn claimant, LocalTargetInfo target, bool allowSameMap, out Map map)
    {
        if (target.HasThing)
        {
            map = target.Thing.MapHeld;
        }
        else
        {
            TargetMapManager.TargetMap.TryGetValue(claimant, out map);
        }
        return map is not null && (allowSameMap || ___map != map);
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.ReservedBy), [typeof(LocalTargetInfo), typeof(Pawn), typeof(Job)])]
public static class Patch_ReservationManager_ReservedBy
{
    public static bool Prefix(Map ___map, Pawn claimant, LocalTargetInfo target, Job job, ref bool __result)
    {
        if (Patch_ReservationManager_Reserve.ShouldReplace(___map, claimant, target, false, out var map))
        {
            __result = map.reservationManager.ReservedBy(target, claimant, job);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserve))]
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
public static class Patch_ReservationManager_CanReserveStack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
        codes[pos] = new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing);

        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
        codes.Insert(pos2, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Map));
        return codes;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.TryGetReserver))]
public static class Patch_ReservationManager_TryGetReserver
{
    public static bool Prefix(Map ___map, LocalTargetInfo target, Faction faction, ref Pawn reserver, ref bool __result)
    {
        Map thingMap;
        if ((thingMap = target.Thing?.MapHeld) != null && ___map != thingMap)
        {
            __result = thingMap.reservationManager.TryGetReserver(target, faction, out reserver);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.FirstRespectedReserver))]
public static class Patch_ReservationManager_FirstRespectedReserver
{
    public static bool Prefix(Map ___map, LocalTargetInfo target, Pawn claimant, ReservationLayerDef layer, ref Pawn __result)
    {
        if (Patch_ReservationManager_Reserve.ShouldReplace(___map, claimant, target, false, out var map))
        {
            __result = map.reservationManager.FirstRespectedReserver(target, claimant, layer);
            return false;
        }
        return true;
    }
}

//FoodSourceの一覧に車上マップの物を含める
[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
public static class Patch_FoodUtility_BestFoodSourceOnMap
{
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
        var maps = getter.Map.BaseMapAndVehicleMaps().Except(getter.Map);
        if (maps.Any())
        {
            searchSet.Clear();
            searchSet.AddRange(list);
            foreach (var map in maps)
            {
                searchSet.AddRange(map.listerThings.ThingsMatching(req));
            }
            return searchSet;
        }
        return list;
    }

    private static List<Thing> searchSet = [];
}

[HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
public static class Patch_RestUtility_CanUseBedNow
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        //!building_Bed.Position.IsInPrisonCell(building_Bed.Map)があるので置き換えるのは最初のMapのみ
        var code = instructions.FirstOrDefault(i => i.opcode == OpCodes.Callvirt && i.OperandIs(CachedMethodInfo.g_Thing_Map));
        code?.operand = CachedMethodInfo.m_BaseMap_Thing;
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
    }
}

[HarmonyPatch(typeof(ToilFailConditions), nameof(ToilFailConditions.DespawnedOrNull))]
public static class Patch_ToilFailConditions_DespawnedOrNull
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(ToilFailConditions), nameof(ToilFailConditions.SelfAndParentsDespawnedOrNull))]
public static class Patch_ToilFailConditions_SelfAndParentsDespawnedOrNull
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch]
public static class Patch_ForbidUtility_IsForbidden
{
    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), typeof(Thing), typeof(Pawn))]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_PositionHeld, CachedMethodInfo.m_PositionHeldOnBaseMap);
    }

    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), typeof(IntVec3), typeof(Pawn))]
    public static void Prefix(ref IntVec3 c, Pawn pawn)
    {
        Map map;
        if ((map = Map) != null || TargetMapManager.HasTargetMap(pawn, out map))
        {
            var basePos = c.ToBaseMapCoord(map);
            if (basePos.InBounds(map.BaseMap()))
            {
                c = basePos;
            }
        }
    }

    public static Map Map { get; set; }
}

[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.DutyLocation))]
public static class Patch_PawnUtility_DutyLocation
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch]
public static class Patch_ToilFailConditions_FailOnSomeonePhysicallyInteracting
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.InnerTypes(typeof(ToilFailConditions)).SelectMany(t =>
        {
            var type = t.IsGenericTypeDefinition ? t.MakeGenericType(typeof(Toil)) : t;
            return type.GetDeclaredMethods();
        }).First(m => m.Name.Contains("<FailOnSomeonePhysicallyInteracting>"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        return codes.Select((c, i) =>
        {
            if (c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map))
            {
                codes[i - 1].opcode = OpCodes.Ldloc_1;
                c.operand = CachedMethodInfo.g_Thing_MapHeld;
            }
            return c;
        });
    }
}

[HarmonyPatch]
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

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

//JobDriver_GotoDestMapはnextJobを使ってReservationを行っているので、それを使って解放しなければならない
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ClearReservationsForJob))]
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
public static class Patch_TransporterUtility_GetTransportersInGroup
{
    public static void Postfix(int transportersGroup, Map map, List<CompTransporter> outTransporters)
    {
        if (transportersGroup < 0)
        {
            return;
        }

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(map.BaseMap()))
        {
            IEnumerable<Thing> list = vehicle.VehicleMap.listerThings.GetAllThings(t => t.HasComp<CompBuildableContainer>());
            foreach (var container in list)
            {
                CompTransporter compTransporter = container.TryGetComp<CompBuildableContainer>();
                if (compTransporter.groupID == transportersGroup)
                {
                    outTransporters.Add(compTransporter);
                }
            }
        }
    }
}

[HarmonyPatch(typeof(ThingOwner), "NotifyAdded")]
public static class Patch_ThingOwner_NotifyAdded
{
    public static void Postfix(Thing item, IThingHolder ___owner)
    {
        if (___owner is Pawn_InventoryTracker inventory && inventory.pawn is VehiclePawnWithMap vehicle)
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
public static class Patch_ThingOwner_NotifyAddedAndMergedWith
{
    public static void Postfix(Thing item, IThingHolder ___owner, int mergedCount)
    {
        if (___owner is Pawn_InventoryTracker inventory && inventory.pawn is VehiclePawnWithMap vehicle)
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
public static class Patch_JobDriver_FoodDeliver_MakeNewToils
{
    public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values, Job ___job)
    {
        var found = false;
        foreach (var toil in values)
        {
            if (toil.debugName == "MakeNewToils" && !found)
            {
                toil.AddPreInitAction(() =>
                {
                    if (___job.targetB.HasThing && toil.actor.Map != ___job.targetB.Thing.MapHeld && toil.actor.CanReach(___job.targetB, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, ___job.targetB.Thing.MapHeld, out var exitSpot, out var enterSpot))
                    {
                        JobAcrossMapsUtility.StartGotoDestMapJob(toil.actor, exitSpot, enterSpot);
                    }
                });
            }
            yield return toil;
        }
    }
}

//billGiverRootCell.GetRegion(pawn.Map, RegionType.Set_Passable); -> billGiverRootCell.GetRegion(billGiver.Map, RegionType.Set_Passable);
[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
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

        var m_BreadthFirstTraverse = AccessTools.Method(typeof(RegionTraverser), nameof(RegionTraverser.BreadthFirstTraverse), [typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType)]);
        var m_BreadthFirstTraverseAcrossMaps = AccessTools.Method(typeof(RegionTraverserAcrossMaps), nameof(RegionTraverserAcrossMaps.BreadthFirstTraverse), [typeof(Region), typeof(RegionEntryPredicate), typeof(RegionProcessor), typeof(int), typeof(RegionType)]);
        return codes.MethodReplacer(m_BreadthFirstTraverse, m_BreadthFirstTraverseAcrossMaps);
    }
}

[HarmonyPatch]
public static class Patch_WorkGiver_DoBill_TryFindBestIngredientsHelper_Predicate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(WorkGiver_DoBill), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name == "<TryFindBestIngredientsHelper>b__0"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch]
public static class Patch_JobDriver_Mine_MakeNewToils_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(JobDriver_Mine), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name == "<MakeNewToils>b__0"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        //先にget_TargetAをstloc.0しとくぞ
        var pos = instructions.FirstIndexOf(c => c.opcode == OpCodes.Stloc_0) - 3;
        if (pos >= 0)
        {
            foreach (var instruction in instructions.Skip(pos).Take(4))
            {
                yield return instruction;
            }
        }
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(CachedMethodInfo.g_Thing_Map))
            {
                yield return CodeInstruction.LoadLocal(0);
                yield return CodeInstruction.Call(typeof(Patch_JobDriver_Mine_MakeNewToils_Delegate), nameof(TargetMap));
            }
            else
            {
                yield return instruction;
            }
        }
    }

    private static Map TargetMap(Thing thing, LocalTargetInfo target)
    {
        return target.Thing?.Map ?? thing.Map;
    }
}