using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SmashTools;
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

  public static Job InsertBoardJobIfNeeded(Pawn pawn)
  {
    if (!pawn.IsOnVehicleMapOf(out var vehicle) || vehicle.HasEnoughOperators) return null;

    var reservationManager = vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>();
    foreach (var handler in vehicle.Handlers)
    {
      if (!handler.AreSlotsAvailableAndReservable ||
          !CanOperateRole(pawn, handler.role.HandlingTypes) ||
          !handler.RequiredForMovement) continue;

      var target = handler.role is VehicleRoleBuildable buildable
        ? buildable.upgradeComp.parent
        : vehicle;
      if (pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, target.Map,
            out var exitSpot, out var enterSpot, out var spotsQueue))
      {
        var job = JobMaker.MakeJob(VMF_DefOf.VMF_BoardAcrossMaps, target)
          .SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, spotsQueue);
        vehicle.GiveLoadJob(pawn, handler);
        reservationManager?.Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, pawn, job, handler);
        return job;
      }
    }
    return null;
    
    static bool CanOperateRole(Pawn pawn, HandlingType handlingType)
    {
      if (handlingType == HandlingType.None)
        return true;

      if ((handlingType & HandlingType.Turret) != 0 && (!pawn.IsPlayerControlled || pawn.WorkTagIsDisabled(WorkTags.Violent)))
        return false;

      if (!pawn.RaceProps.ToolUser)
        return false;

      if (pawn.Downed || pawn.Dead || pawn.IsPlayerControlled && pawn.InMentalState)
        return false;

      if (pawn.IsPrisoner || pawn.IsColonyMech)
        return false;

      if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        return false;

      if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness))
        return false;

      return true;
    }
  }
}