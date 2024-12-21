using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using VehicleInteriors.Jobs.WorkGivers;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
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

    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_JobGiver_Work_TryIssueJobPackage
    {
        //目的のthingとpawnのMapが違った場合目的のマップに行くJobにすり替える
        //public static void Postfix(ref ThinkResult __result, Pawn pawn)
        //{
        //    if (__result == ThinkResult.NoJob || (__result.Job.workGiverDef?.Worker is WorkGiver_Scanner scanner && scanner.AllowUnreachable)) return;

        //    var driver = __result.Job.GetCachedDriver(pawn);
        //    if (!(driver is JobDriverAcrossMaps))
        //    {
        //        var thing = __result.Job.targetA.Thing ?? __result.Job.targetQueueA.FirstOrDefault().Thing;
        //        if (thing != null && !__result.Job.targetB.IsValid && pawn.Map != thing.MapHeld && pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, thing.Map, out var exitSpot, out var enterSpot))
        //        {
        //            var job = JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, __result.Job);
        //            __result = new ThinkResult(job, __result.SourceNode, __result.Tag, __result.FromQueue);
        //        }
        //    }
        //}

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            //サーチセットに複数マップのthingリストを足す
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 17);

            var pos2 = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
            var ldfld_scanner = codes[pos2 + 1];

            var pos3 = codes.FindLastIndex(pos2 - 1, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
            var ldfld_innerClass = codes[pos3 + 1];
            var ldfld_pawn = codes[pos3 + 2];

            var addedCodes = new[]
            {
                CodeInstruction.LoadLocal(9),
                ldfld_innerClass,
                ldfld_pawn,
                CodeInstruction.LoadLocal(9),
                ldfld_scanner,
                CodeInstruction.Call(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.AddSearchSet))
            };
            codes.InsertRange(pos, addedCodes);

            var pos4 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            //codes[pos4] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PositionOnBaseMap);

            var pos5 = codes.FindIndex(pos4, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 19);
            codes.InsertRange(pos5, addedCodes);

            //var pos6 = codes.FindIndex(pos5, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            //codes[pos6] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PositionOnBaseMap);

            //GenClosestの各メソッドを自作のものに置き換える
            //PotentialWorkThingsGlobalの各マップの結果を合計
            var m_GenClosest_ClosestThing_Global = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
            var m_GenClosestOnVehicle_ClosestThing_Global = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThing_Global),
                new[] { typeof(IntVec3), typeof(IEnumerable<>), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>) } );
            var m_GenClosest_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
            var m_GenClosestOnVehicle_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThing_Global_Reachable),
                new[] { typeof(IntVec3), typeof(Map), typeof(IEnumerable<Thing>), typeof(PathEndMode), typeof(TraverseParms), typeof(float), typeof(Predicate<Thing>), typeof(Func<Thing, float>) });
            var m_GenClosest_ClosestThingReachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
            var m_GenClosestOnVehicle_ClosestThingReachable = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThingReachable),
                new[] { typeof(IntVec3), typeof(Map), typeof(ThingRequest), typeof(PathEndMode), typeof(TraverseParms), typeof(float), typeof(Predicate<Thing>), typeof(IEnumerable<Thing>), typeof(int), typeof(int), typeof(bool), typeof(RegionType), typeof(bool)});
            var m_Scanner_PotentialWorkThingsGlobal = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal));
            var m_PotentialWorkThingsGlobalAll = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.PotentialWorkThingsGlobalAll));
            var m_Scanner_JobOnThing = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.JobOnThing));
            var m_JobOnThingMap = AccessTools.Method(typeof(Patch_JobGiver_Work_TryIssueJobPackage), nameof(Patch_JobGiver_Work_TryIssueJobPackage.JobOnThingMap));
            return codes.MethodReplacer(m_GenClosest_ClosestThing_Global, m_GenClosestOnVehicle_ClosestThing_Global)
                .MethodReplacer(m_GenClosest_ClosestThing_Global_Reachable, m_GenClosestOnVehicle_ClosestThing_Global_Reachable)
                .MethodReplacer(m_GenClosest_ClosestThingReachable, m_GenClosestOnVehicle_ClosestThingReachable)
                .MethodReplacer(m_Scanner_PotentialWorkThingsGlobal, m_PotentialWorkThingsGlobalAll)
                .MethodReplacer(m_Scanner_JobOnThing, m_JobOnThingMap);
        }

        private static IEnumerable<Thing> AddSearchSet(List<Thing> list, Pawn pawn, WorkGiver_Scanner scanner)
        {
            var searchSet = new List<Thing>(list);
            var baseMap = pawn.BaseMap();
            var maps = VehiclePawnWithMapCache.allVehicles[baseMap].Select(v => v.interiorMap).Concat(baseMap).Except(pawn.Map);
            foreach(var map in maps)
            {
                searchSet.AddRange(map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest));
            }
            return searchSet;
        }

        private static IEnumerable<Thing> PotentialWorkThingsGlobalAll(WorkGiver_Scanner scanner, Pawn pawn)
        {
            var map = pawn.Map;
            var anyNotNull = false;
            var enumerable = pawn.Map.BaseMapAndVehicleMaps().SelectMany(m =>
            {
                pawn.VirtualMapTransfer(m);
                var things = scanner.PotentialWorkThingsGlobal(pawn);
                if (things != null)
                {
                    anyNotNull = true;
                }
                else
                {
                    things = Enumerable.Empty<Thing>(); //こうしないとnullがconcatされて困るっぽい
                }
                return things;
            }).ToList();
            pawn.VirtualMapTransfer(map);
            return anyNotNull ? enumerable : null; //こうしないとPotentialWorkThingRequestによるサーチセット作成が行われないよ
        }

        //マップと位置を仮想移動して出てきたJobをGotoDestMapでくるむ
        private static Job JobOnThingMap(WorkGiver_Scanner scanner, Pawn pawn, Thing t, bool forced)
        {
            if (pawn.Map == t.MapHeld || scanner is IWorkGiverAcrossMaps workGiverAcrossMaps && !workGiverAcrossMaps.NeedWrapWithGotoDestJob)
            {
                return scanner.JobOnThing(pawn, t, forced);
            }

            var map = pawn.Map;
            if (!scanner.AllowUnreachable)
            {
                if (pawn.CanReach(t, scanner.PathEndMode, scanner.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, t.MapHeld, out var exitSpot, out var enterSpot))
                {
                    var pos = pawn.Position;
                    var dest = enterSpot.IsValid ? enterSpot.Cell : exitSpot.Cell;
                    pawn.VirtualMapTransfer(t.MapHeld, dest);
                    var job = scanner.JobOnThing(pawn, t, forced);
                    pawn.VirtualMapTransfer(map, pos);

                    var thing = job.targetA.Thing ?? job.targetQueueA.FirstOrDefault().Thing;
                    if (thing != null && pawn.Map != thing.MapHeld)
                    {
                        return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job);
                    }
                    return job;
                }
                return null;
            }
            pawn.VirtualMapTransfer(t.MapHeld);
            var job2 = scanner.JobOnThing(pawn, t, forced);
            pawn.VirtualMapTransfer(map);
            return job2;
        }
    }

    //ShouldSkipはvehicleMapを含めた全てのマップでスキップするかチェックする
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class Patch_JobGiver_Work_PawnCanUseWorkGiver
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_WorkGiver_ShouldSkip = AccessTools.Method(typeof(WorkGiver), nameof(WorkGiver.ShouldSkip));
            var m_ShouldSkipAll = AccessTools.Method(typeof(Patch_JobGiver_Work_PawnCanUseWorkGiver), nameof(Patch_JobGiver_Work_PawnCanUseWorkGiver.ShouldSkipAll));
            return instructions.MethodReplacer(m_WorkGiver_ShouldSkip, m_ShouldSkipAll);
        }

        private static bool ShouldSkipAll(WorkGiver workGiver, Pawn pawn, bool forced)
        {
            var map = pawn.Map;
            var result = pawn.Map.BaseMapAndVehicleMaps().All(m =>
            {
                pawn.VirtualMapTransfer(m);
                var skip = workGiver.ShouldSkip(pawn, forced);
                return skip;
            });
            pawn.VirtualMapTransfer(map);
            return result;
        }
    }

    [HarmonyPatch]
    public static class Patch_JobGiver_Work_Validator
    {
        public static MethodInfo TargetMethod()
        {
            return AccessTools.InnerTypes(typeof(JobGiver_Work)).SelectMany(t => t.GetMethods(AccessTools.all)).First(m => m.Name.Contains("Validator"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_ForbidUtility_IsForbidden, MethodInfoCache.m_ReservationAcrossMapsUtility_IsForbidden)
                .MethodReplacer(m_Scanner_HasJobOnThing, m_HasJobOnThingMap);
        }

        //目的のtに届く位置とマップに転移してからHasJobOnThingを走らせる
        private static bool HasJobOnThingMap(WorkGiver_Scanner scanner, Pawn pawn, Thing t, bool forced)
        {
            if (pawn.Map == t.MapHeld)
            {
                return scanner.HasJobOnThing(pawn, t, forced);
            }

            var map = pawn.Map;
            if (!scanner.AllowUnreachable)
            {
                if (pawn.CanReach(t, scanner.PathEndMode, scanner.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, t.MapHeld, out var exitSpot, out var enterSpot))
                {
                    var pos = pawn.Position;
                    var dest = enterSpot.IsValid ? enterSpot.Cell : exitSpot.Cell;
                    pawn.VirtualMapTransfer(t.MapHeld, dest);
                    var hasJob = scanner.HasJobOnThing(pawn, t, forced);
                    pawn.VirtualMapTransfer(map, pos);
                    return hasJob;
                }
                return false;
            }
            pawn.VirtualMapTransfer(t.MapHeld);
            var hasJob2 = scanner.HasJobOnThing(pawn, t, forced);
            pawn.VirtualMapTransfer(map);
            return hasJob2;
        }

        private static readonly MethodInfo m_Scanner_HasJobOnThing = AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.HasJobOnThing));

        private static readonly MethodInfo m_HasJobOnThingMap = AccessTools.Method(typeof(Patch_JobGiver_Work_Validator), nameof(Patch_JobGiver_Work_Validator.HasJobOnThingMap));
    }


    [HarmonyPatch]
    public static class Patch_JobGiver_Work_GiverTryGiveJobPrioritized
    {
        public static MethodInfo TargetMethod()
        {
            return AccessTools.InnerTypes(typeof(JobGiver_Work)).SelectMany(t => t.GetMethods(AccessTools.all)).First(m => m.Name.Contains("<GiverTryGiveJobPrioritized>"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_JobGiver_Work_Validator.Transpiler(instructions);
    }

    //利用可能なthingに車上マップ上のthingを含める
    [HarmonyPatch(typeof(ItemAvailability), nameof(ItemAvailability.ThingsAvailableAnywhere))]
    public static class Patch_ItemAvailability_ThingsAvailableAnywhere
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var pos = code.FindIndex(c => c.opcode == OpCodes.Stloc_2);
            code.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(ItemAvailability), "map"),
                CodeInstruction.LoadArgument(1),
                CodeInstruction.Call(typeof(Patch_ItemAvailability_ThingsAvailableAnywhere), nameof(Patch_ItemAvailability_ThingsAvailableAnywhere.AddThingList))
            });
            return code;
        }

        private static List<Thing> AddThingList(List<Thing> list, Map map, ThingDef need)
        {
            var result = new List<Thing>(list);
            foreach (var vehicle in VehiclePawnWithMapCache.allVehicles[map])
            {
                result.AddRange(vehicle.interiorMap.listerThings.ThingsOfDef(need));
            }
            return result;
        }
    }
}
