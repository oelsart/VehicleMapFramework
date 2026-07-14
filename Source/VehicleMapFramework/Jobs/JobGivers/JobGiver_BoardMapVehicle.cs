using RimWorld;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobGiver_BoardMapVehicle : ThinkNode_JobGiver
{
  protected override Job TryGiveJob(Pawn pawn)
  {
    if (!pawn.IsOnVehicleMapOf(out var vehicle) ||
        pawn.Faction != vehicle.Faction ||
        vehicle.HasEnoughOperators) return null;

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