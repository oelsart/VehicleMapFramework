using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_RefuelAcrossMaps : JobDriverAcrossMaps
    {
        private const TargetIndex RefuelableInd = TargetIndex.A;

        private const TargetIndex FuelInd = TargetIndex.B;

        public const int RefuelingDuration = 240;

        protected Thing Refuelable => job.GetTarget(TargetIndex.A).Thing;

        protected CompRefuelable RefuelableComp => Refuelable.TryGetComp<CompRefuelable>();

        protected Thing Fuel => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var refuelable = this.Refuelable;
            if (pawn.Reserve(refuelable.MapHeld, refuelable, job, 1, -1, null, errorOnFailed))
            {
                var fuel = this.Fuel;
                return pawn.Reserve(fuel.MapHeld, fuel, job, 1, -1, null, errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            AddEndCondition(() => (!RefuelableComp.IsFull) ? JobCondition.Ongoing : JobCondition.Succeeded);
            AddFailCondition(() => !job.playerForced && !RefuelableComp.ShouldAutoRefuelNowIgnoringFuelPct);
            AddFailCondition(() => !RefuelableComp.allowAutoRefuel && !job.playerForced);
            yield return Toils_General.DoAtomic(delegate
            {
                job.count = RefuelableComp.GetFuelCountToFullyRefuel();
            });
            Toil reserveFuel = Toils_Reserve.Reserve(TargetIndex.B);
            yield return reserveFuel;
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.B)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(240).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);
            yield return Toils_Refuel.FinalizeRefueling(TargetIndex.A, TargetIndex.B);
        }
    }
}
