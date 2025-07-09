using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class JobAcrossMapsUtility
{
    public static void StartGotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null)
    {
        var nextJob = pawn.CurJob.Clone();
        var driver = nextJob.GetCachedDriver(pawn);

        //QueueBを使い果たした後でDoBillを再開するといらんとこまでJumpIfしてしまうためその対策
        if (driver is JobDriver_DoBill && pawn.CurJob.targetQueueB.NullOrEmpty() && (pawn.IsCarrying() || pawn.CurJob.targetB.HasThing))
        {
            nextJob.AddQueuedTarget(TargetIndex.B, pawn.carryTracker.CarriedThing ?? pawn.CurJob.targetB.Thing);
        }

        curToilIndex(driver) = pawn.jobs.curDriver.CurToilIndex - 1;
        pawn.jobs.curDriver.globalFinishActions.Clear(); //Jobはまだ終わっちゃいねえためFinishActionはさせない。TryDropThingなどをしていることもあるし
        var job = GotoDestMapJob(pawn, exitSpot, enterSpot, nextJob);
        job.playerForced = nextJob.playerForced;
        pawn.jobs.StartJob(job, JobCondition.InterruptForced, keepCarryingThingOverride: true);
    }

    private static AccessTools.FieldRef<JobDriver, int> curToilIndex = AccessTools.FieldRefAccess<JobDriver, int>("curToilIndex");

    public static Job GotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null, Job nextJob = null)
    {
        if ((enterSpot.HasValue && enterSpot.Value.Map != null) || (exitSpot.HasValue && exitSpot.Value.Map != null))
        {
            return JobMaker.MakeJob(VMF_DefOf.VMF_GotoDestMap).SetSpotsAndNextJob(pawn, exitSpot, enterSpot, nextJob: nextJob);
        }
        return nextJob;
    }

    [Obsolete]
    public static void TryTakeGotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null)
    {
        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
    }

    public static Job SetSpotsToJobAcrossMaps(this Job job, Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null)
    {
        var driver = job.GetCachedDriver(pawn) as JobDriverAcrossMaps;
        driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
        return job;
    }

    public static Job SetSpotsAndNextJob(this Job job, Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null, Job nextJob = null)
    {
        var driver = job.GetCachedDriver(pawn) as JobDriver_GotoDestMap;
        driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
        driver.nextJob = nextJob;
        return job;
    }

    public static Job NextJobOfGotoDestmapJob(Pawn pawn)
    {
        var driver = pawn.jobs.curDriver as JobDriver_GotoDestMap;
        return driver?.nextJob;
    }

    public static bool PawnDeterminingJob(this Pawn pawn)
    {
        return pawn.jobs.DeterminingNextJob || FloatMenuMakerMap.makingFor == pawn;
    }

    public static bool NoNeedVirtualMapTransfer(Map pawnMap, Map targetMap, WorkGiver_Scanner scanner)
    {
        return pawnMap == targetMap ||
            scanner is IWorkGiverAcrossMaps workGiverAcrossMaps && !workGiverAcrossMaps.NeedVirtualMapTransfer ||
            scanner is WorkGiver_DoBill ||
            scanner is WorkGiver_ConstructDeliverResources ||
            scanner is WorkGiver_ConstructFinishFrames ||
            scanner is WorkGiver_Refuel ||
            scanner is WorkGiver_LoadTransporters;
    }
}