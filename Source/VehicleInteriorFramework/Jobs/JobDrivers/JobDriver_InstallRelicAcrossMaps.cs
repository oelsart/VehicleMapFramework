using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VehicleInteriors
{
    public class JobDriver_InstallRelicAcrossMaps : JobDriverAcrossMaps
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetA.Thing.Map, this.job.GetTarget(TargetIndex.A), this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.DestMap, this.job.GetTarget(TargetIndex.B), this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (!ModLister.CheckIdeology("Relic"))
            {
                yield break;
            }
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil2 in this.GotoTargetMap(RelicInd)) yield return toil2;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell, false).FailOnDespawnedNullOrForbidden(TargetIndex.A).FailOn((Toil to) => ReliquaryFull());
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, true, false, true, false).FailOn((Toil to) => ReliquaryFull());
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil2 in this.GotoTargetMap(ContainerInd)) yield return toil2;
            }
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.C, PathEndMode.ClosestTouch).FailOn((Toil to) => ReliquaryFull());
            Toil toil = Toils_General.Wait(300, TargetIndex.B).WithProgressBarToilDelay(TargetIndex.B, false, -0.5f).FailOnDespawnedOrNull(TargetIndex.B).FailOn((Toil to) => ReliquaryFull());
            toil.handlingFacing = true;
            yield return toil;
            yield return Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.A, delegate
            {
                this.job.GetTarget(TargetIndex.A).Thing.def.soundDrop.PlayOneShot(new TargetInfo(this.job.GetTarget(TargetIndex.B).Cell, this.pawn.Map, false));
                SoundDefOf.Relic_Installed.PlayOneShot(new TargetInfo(this.job.GetTarget(TargetIndex.B).Cell, this.pawn.Map, false));
            });
            yield break;
        }

        private bool ReliquaryFull()
        {
            CompRelicContainer compRelicContainer = this.pawn.jobs.curJob.GetTarget(TargetIndex.B).Thing.TryGetComp<CompRelicContainer>();
            return compRelicContainer == null || compRelicContainer.Full;
        }

        private const TargetIndex RelicInd = TargetIndex.A;

        private const TargetIndex ContainerInd = TargetIndex.B;

        private const TargetIndex ContainerInteractionCellInd = TargetIndex.C;

        private const int InstallTicks = 300;
    }
}
