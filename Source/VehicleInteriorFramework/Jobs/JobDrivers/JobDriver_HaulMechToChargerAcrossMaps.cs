using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_HaulMechToChargerAcrossMaps : JobDriverAcrossMaps
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetB.Thing.Map, this.job.GetTarget(TargetIndex.B), this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.job.targetA.Thing.Map, this.job.GetTarget(TargetIndex.A), this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil3 in this.GotoTargetMap(MechInd)) yield return toil3;
            }
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return toil;
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil3 in this.GotoTargetMap(ChargerInd)) yield return toil3;
            }
            Toil toil2 = Toils_Haul.CarryHauledThingToCell(TargetIndex.C, PathEndMode.OnCell);
            yield return toil2;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, null, false, false);
            yield return Toils_General.Do(delegate
            {
                this.job.targetB.Thing.Map.reservationManager.Release(this.job.targetB, this.pawn, this.job);
                Pawn pawn = (Pawn)this.job.targetA.Thing;
                Building_MechCharger t = (Building_MechCharger)this.job.targetB.Thing;
                Job newJob = JobMaker.MakeJob(JobDefOf.MechCharge, t);
                pawn.jobs.StartJob(newJob, JobCondition.InterruptForced, null, false, true, null, null, false, false, null, false, true, false);
            });
            yield break;
        }

        private const TargetIndex MechInd = TargetIndex.A;

        private const TargetIndex ChargerInd = TargetIndex.B;

        private const TargetIndex ChargerCellInd = TargetIndex.C;
    }
}
