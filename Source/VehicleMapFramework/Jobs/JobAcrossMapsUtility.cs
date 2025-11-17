using System;
using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class JobAcrossMapsUtility
{
    private static readonly AccessTools.FieldRef<JobDriver, int> curToilIndex = AccessTools.FieldRefAccess<JobDriver, int>("curToilIndex");

    public static void StartGotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null)
    {
        if (pawn.CurJobDef == VMF_DefOf.VMF_GotoDestMap) return;

        var nextJob = pawn.CurJob.Clone();
        var driver = nextJob.GetCachedDriver(pawn);
        curToilIndex(driver) = pawn.jobs.curDriver.CurToilIndex - 1;
        pawn.jobs.curDriver.globalFinishActions.Clear(); //Jobはまだ終わっちゃいねえためFinishActionはさせない。TryDropThingなどをしていることもあるし
        var job = GotoDestMapJob(pawn, exitSpot, enterSpot, nextJob);
        job.playerForced = nextJob.playerForced;
        pawn.jobs.StartJob(job, JobCondition.InterruptForced, keepCarryingThingOverride: true, preToilReservationsCanFail: true);
    }

    public static Job GotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null, Job nextJob = null)
    {
        if (enterSpot is { Map: not null } || exitSpot is { Map: not null })
        {
            return JobMaker.MakeJob(VMF_DefOf.VMF_GotoDestMap).SetSpotsAndNextJob(pawn, exitSpot, enterSpot, nextJob: nextJob);
        }
        return nextJob;
    }

    [Obsolete]
    public static void TryTakeGotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null)
    {
        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot), JobTag.Misc);
    }

    extension(Job job)
    {
        public Job SetSpotsToJobAcrossMaps(Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null)
        {
            if (job.GetCachedDriver(pawn) is not JobDriverAcrossMaps driver) return null;
            driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
            return job;
        }

        public Job SetSpotsAndNextJob(Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null, Job nextJob = null)
        {
            if (job.GetCachedDriver(pawn) is not JobDriver_GotoDestMap driver) return null;
            driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
            driver.nextJob = nextJob;
            return job;
        }
    }

    public static Job NextJobOfGotoDestMapJob(Pawn pawn)
    {
        var driver = pawn.jobs.curDriver as JobDriver_GotoDestMap;
        return driver?.nextJob;
    }

    public static bool NoNeedVirtualMapTransfer(Map pawnMap, Map targetMap)
    {
        return pawnMap == targetMap;
    }

    public static bool NeedWrapGotoDestMapJob(WorkGiver_Scanner scanner)
    {
        return scanner is WorkGiver_HunterHunt or WorkGiver_Miner or VehicleWorkGiver;
    }
}