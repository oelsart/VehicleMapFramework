using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[StaticConstructorOnStartup]
public class VehicleTurret_AutoRefuel : VehicleTurret
{
    /// <summary>
    /// Init from CompProperties
    /// </summary>
    public VehicleTurret_AutoRefuel()
    {
    }

    /// <summary>
    /// Init from save file
    /// </summary>
    public VehicleTurret_AutoRefuel(VehiclePawn vehicle) : base(vehicle)
    {
    }

    /// <summary>
    /// Newly Spawned
    /// </summary>
    /// <param name="vehicle"></param>
    /// <param name="reference">VehicleTurret as defined in xml</param>
    public VehicleTurret_AutoRefuel(VehiclePawn vehicle, VehicleTurret reference) : base(vehicle, reference)
    {
    }

    static VehicleTurret_AutoRefuel()
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            RefuelVehicleTurret = (WorkGiver_RefuelVehicleTurret)DefDatabase<WorkGiverDef>.GetNamed("PackVehicleTurret").Worker;
        });
    }
    
    private static WorkGiver_RefuelVehicleTurret RefuelVehicleTurret { get; set; }

    public override void PostTurretFire()
    {
        base.PostTurretFire();
        if (loadedAmmo is null)
        {
            if (vehicle.Map.reservationManager.ReservationsReadOnly.Any(r => r.Job is not null &&
                    r.Job.workGiverDef == RefuelVehicleTurret.def && r.Job.targetB == vehicle))
                return;
            
            var handler =
                vehicle.handlers.FirstOrDefault(handler => (handler.role.HandlingTypes & HandlingType.Turret) ==
                                                           HandlingType.Turret &&
                                                           (handler.role.TurretIds.Contains(key) ||
                                                            handler.role.TurretIds.Contains(groupKey)));
            var pawn = handler?.thingOwner.InnerListForReading.FirstOrDefault();
            if (pawn is null) return;
            vehicle.DisembarkPawn(pawn);
            var job = RefuelVehicleTurret.JobOnThing(pawn, vehicle);
            if (job is null) return;
            pawn.jobs.TryTakeOrderedJob(job);
            var job2 =  new Job(JobDefOf_Vehicles.Board, vehicle);
            vehicle.GiveLoadJob(pawn, handler);
            pawn.jobs.TryTakeOrderedJob(job2, requestQueueing: true);
            vehicle.Map.GetCachedMapComponent<VehicleReservationManager>()
                .Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, pawn, job2, handler);
        }
    }
}