using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class JobAcrossMapsUtility
{
  private static readonly AccessTools.FieldRef<JobDriver, int> curToilIndex =
    AccessTools.FieldRefAccess<JobDriver, int>("curToilIndex");

  public static List<Type> WorkGiverClassesNonScanAll { get; } =
  [
    typeof(WorkGiver_Fish)
  ];

  public static List<Type> WorkGiverClassesNeedWrap { get; } = [];

  public static List<Type> JobDriverClassesNeedWrap { get; } =
  [
    typeof(JobDriver_RemoveFloor)
  ];

  public static List<WorkGiverDef> DisabledCrossMapWorkGiverDefs { get; } = [];

  public static void StartGotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null,
    List<TraverseSpots> spotsQueue = null)
  {
    if (pawn.jobs is null or { curDriver: JobDriverAcrossMaps })
      return;

    var nextJob = pawn.CurJob.Clone();
    var driver = nextJob.GetCachedDriver(pawn);
    curToilIndex(driver) = pawn.jobs.curDriver.CurToilIndex - 1;
    pawn.jobs.curDriver.globalFinishActions.Clear(); //Jobはまだ終わっちゃいねえためFinishActionはさせない。TryDropThingなどをしていることもあるし
    var job = GotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue, nextJob);
    job.playerForced = nextJob.playerForced;
    pawn.jobs.StartJob(job, JobCondition.InterruptForced, keepCarryingThingOverride: true,
      preToilReservationsCanFail: true);
  }

  public static Job GotoDestMapJob(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null,
    List<TraverseSpots> spotsQueue = null, Job nextJob = null)
  {
    if (!spotsQueue.NullOrEmpty() &&
        spotsQueue.Any(s => s.exitSpot is { Map: not null } || s.enterSpot is { Map: not null }))
      return JobMaker.MakeJob(VMF_DefOf.VMF_GotoDestMap).SetSpotsAndNextJob(pawn, spotsQueue, nextJob: nextJob);
    if (enterSpot is { Map: not null } || exitSpot is { Map: not null })
      return JobMaker.MakeJob(VMF_DefOf.VMF_GotoDestMap)
        .SetSpotsAndNextJob(pawn, exitSpot, enterSpot, nextJob: nextJob);
    return nextJob;
  }

  extension(Job job)
  {
    public Job SetSpotsToJobAcrossMaps(Pawn pawn, TargetInfo? exitSpot = null, TargetInfo? enterSpot = null,
      List<TraverseSpots> spotsQueue = null)
    {
      if (job.GetCachedDriver(pawn) is not JobDriverAcrossMaps driver) return null;
      if (spotsQueue.NullOrEmpty()) driver.SetSpots(exitSpot, enterSpot);
      else driver.SetSpots(spotsQueue);
      return job;
    }

    public Job SetSpotsAndNextJob(Pawn pawn, List<TraverseSpots> spotsQueueA = null,
      List<TraverseSpots> spotsQueueB = null, Job nextJob = null)
    {
      if (job.GetCachedDriver(pawn) is not JobDriver_GotoDestMap driver) return null;
      driver.SetSpots(spotsQueueA, spotsQueueB);
      driver.nextJob = nextJob;
      return job;
    }

    public Job SetSpotsAndNextJob(Pawn pawn, TargetInfo? exitSpotA = null, TargetInfo? enterSpotA = null,
      TargetInfo? exitSpotB = null, TargetInfo? enterSpotB = null, Job nextJob = null)
    {
      if (job.GetCachedDriver(pawn) is not JobDriver_GotoDestMap driver) return null;
      driver.SetSpots(exitSpotA, enterSpotA, exitSpotB, enterSpotB);
      driver.nextJob = nextJob;
      return job;
    }
  }

  public static Job NextJobOfGotoDestMapJob(Pawn pawn)
  {
    var driver = pawn.jobs.curDriver as JobDriver_GotoDestMap;
    return driver?.nextJob;
  }

  public static bool NoNeedVirtualMapTransfer(Map pawnMap, Map targetMap, WorkGiverDef workGiver)
  {
    return pawnMap == targetMap || !pawnMap.CrossMapContext || DisabledCrossMapWorkGiverDefs.Contains(workGiver);
  }

  public static bool NoNeedWrapGotoDestMapJob(WorkGiver_Scanner scanner)
  {
    return scanner is WorkGiver_PaintFloor;
  }

  public static bool NeedWrapGotoDestMapJob(WorkGiver_Scanner scanner, Job job = null)
  {
    return scanner is WorkGiver_Merge or WorkGiver_HunterHunt or WorkGiver_Miner or VehicleWorkGiver ||
           job is not null && JobDriverClassesNeedWrap.Contains(job.def.driverClass) ||
           WorkGiverClassesNeedWrap.Contains(scanner.def.giverClass);
  }
}