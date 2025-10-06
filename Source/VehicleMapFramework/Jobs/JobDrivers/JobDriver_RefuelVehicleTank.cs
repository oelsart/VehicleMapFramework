using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_RefuelVehicleTank : JobDriver
{
    protected Thing Tank
    {
        get
        {
            return job.GetTarget(TargetIndex.A).Thing;
        }
    }

    protected VehiclePawn Vehicle
    {
        get
        {
            if (Tank.IsOnVehicleMapOf(out var vehicle)) return vehicle;
            return null;
        }
    }

    protected Thing Fuel
    {
        get
        {
            return job.GetTarget(TargetIndex.B).Thing;
        }
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Tank, job, 1, -1, null, errorOnFailed) && pawn.Reserve(Fuel, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        AddEndCondition(delegate
        {
            var compFueledTravel = Vehicle?.CompFueledTravel;
            if (compFueledTravel is null)
            {
                return JobCondition.Incompletable;
            }
            if (!compFueledTravel.FullTank)
            {
                return JobCondition.Ongoing;
            }
            return JobCondition.Succeeded;
        });
        yield return Toils_General.DoAtomic(delegate
        {
            job.count = Vehicle.CompFueledTravel.FuelCountToFull;
        });
        var reserveFuel = Toils_Reserve.Reserve(TargetIndex.B);
        yield return reserveFuel;
        yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
        yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true).FailOnDestroyedNullOrForbidden(TargetIndex.B);
        yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, TargetIndex.B, TargetIndex.None, true);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        yield return Toils_General.Wait(RefuelingDuration).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A);
        yield return FinalizeRefueling(TargetIndex.A, TargetIndex.B);
    }

    public static Toil FinalizeRefueling(TargetIndex refuelableInd, TargetIndex fuelInd)
    {
        Toil toil = new();
        toil.initAction = delegate
        {
            var curJob = toil.actor.CurJob;
            Thing thing = curJob.GetTarget(refuelableInd).Thing.TryGetComp<CompFuelTank>().Vehicle;
            if (toil.actor.CurJob.placedThings.NullOrEmpty())
            {
                thing?.TryGetComp<CompFueledTravel>().Refuel(
                [
                    curJob.GetTarget(fuelInd).Thing
                ]);
                return;
            }
            thing?.TryGetComp<CompFueledTravel>().Refuel([.. from p in toil.actor.CurJob.placedThings
                                                         select p.thing]);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }

    private const int RefuelingDuration = 240;
}
