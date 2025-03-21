using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_RefuelVehicleTank : JobDriver
    {
        protected Thing Tank
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected VehiclePawn Vehicle
        {
            get
            {
                return Tank.TryGetComp<CompFuelTank>().Vehicle;
            }
        }

        protected Thing Fuel
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Tank, this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.Fuel, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            base.AddEndCondition(delegate
            {
                if (!this.Vehicle.CompFueledTravel.FullTank)
                {
                    return JobCondition.Ongoing;
                }
                return JobCondition.Succeeded;
            });
            yield return Toils_General.DoAtomic(delegate
            {
                this.job.count = this.Vehicle.CompFueledTravel.FuelCountToFull;
            });
            Toil reserveFuel = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null, false);
            yield return reserveFuel;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch, false).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
            yield return Toils_General.Wait(RefuelingDuration, TargetIndex.None).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return JobDriver_RefuelVehicleTank.FinalizeRefueling(TargetIndex.A, TargetIndex.B);
        }

        public static Toil FinalizeRefueling(TargetIndex refuelableInd, TargetIndex fuelInd)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Job curJob = toil.actor.CurJob;
                Thing thing = curJob.GetTarget(refuelableInd).Thing.TryGetComp<CompFuelTank>().Vehicle;
                if (toil.actor.CurJob.placedThings.NullOrEmpty())
                {
                    thing.TryGetComp<CompFueledTravel>().Refuel(new List<Thing>
                    {
                        curJob.GetTarget(fuelInd).Thing
                    });
                    return;
                }
                thing.TryGetComp<CompFueledTravel>().Refuel((from p in toil.actor.CurJob.placedThings
                                                             select p.thing).ToList<Thing>());
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private const int RefuelingDuration = 240;
    }
}
