﻿using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_RefuelAtomicAcrossMaps : JobDriverAcrossMaps
    {
        private const TargetIndex RefuelableInd = TargetIndex.A;

        private const TargetIndex FuelInd = TargetIndex.B;

        private const TargetIndex FuelPlaceCellInd = TargetIndex.C;

        private const int RefuelingDuration = 240;

        protected Thing Refuelable => job.GetTarget(TargetIndex.A).Thing;

        protected CompRefuelable RefuelableComp => Refuelable.TryGetComp<CompRefuelable>();

        protected Thing Fuel => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(DestMap, job.GetTargetQueue(TargetIndex.B), job);
            var refuelable = this.Refuelable;
            return pawn.Reserve(TargetAMap, refuelable, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            AddEndCondition(() => (!RefuelableComp.IsFull) ? JobCondition.Ongoing : JobCondition.Succeeded);
            AddFailCondition(() => (!job.playerForced && !RefuelableComp.ShouldAutoRefuelNowIgnoringFuelPct) || !RefuelableComp.allowAutoRefuel);
            AddFailCondition(() => !RefuelableComp.allowAutoRefuel && !job.playerForced);
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.B)) yield return toil;
            }
            yield return Toils_General.DoAtomic(delegate
            {
                job.count = RefuelableComp.GetFuelCountToFullyRefuel();
            });
            Toil getNextIngredient = Toils_General.Label();
            yield return getNextIngredient;
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(TargetIndex.A, TargetIndex.B, TargetIndex.C);
            yield return findPlaceTarget;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, findPlaceTarget, storageMode: false);
            yield return Toils_Jump.JumpIf(getNextIngredient, () => !job.GetTargetQueue(TargetIndex.B).NullOrEmpty());
            yield return Toils_General.Wait(240).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);
            yield return Toils_Refuel.FinalizeRefueling(TargetIndex.A, TargetIndex.B);
        }
    }
}
