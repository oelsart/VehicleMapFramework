using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class WorkGiver_DriveVehicle : WorkGiver_Scanner
{
  private const int ExpiryInterval = 600;

  public override bool ShouldSkip(Pawn pawn, bool forced = false) => !pawn.IsOnVehicleMap;

  public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
  {
    if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.ParentHolder is VehicleCaravan { vehiclePather.Moving: true })
      return vehicle.VehicleSeatComps.Select(s => s.parent);
    return [];
  }

  public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
  {
    if (!t.IsOnVehicleMap || !t.TryGetComp<CompVehicleSeat>(out var seatComp))
      return false;

    foreach (var (handler, _) in seatComp.handlers)
    {
      if (handler is { RequiredForMovement: true, AreSlotsAvailableAndReservable: true, RoleFulfilled: false } &&
          handler.CanOperateRole(pawn))
        return true;
    }

    return false;
  }

  public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
  {
    if (!t.IsOnVehicleMapOf(out var vehicle) || !t.TryGetComp<CompVehicleSeat>(out var seatComp))
      return null;

    var handler = seatComp.handlers.Find(h =>
      h.handler is { RequiredForMovement: true, AreSlotsAvailableAndReservable: true, RoleFulfilled: false } &&
      h.handler.CanOperateRole(pawn)).handler;
    if (handler is null)
      return null;
    
    vehicle.GiveLoadJob(pawn, handler);
    var job = JobMaker.MakeJob(VMF_DefOf.VMF_BoardAcrossMaps, t, ExpiryInterval);
    vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>()?
      .Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, pawn, job, handler);
    return job;
  }
}