using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_TransferBetweenEntityHoldersAcrossMaps : JobDriverAcrossMaps
    {
        private Thing Takee
        {
            get
            {
                return this.job.GetTarget(TargetIndex.C).Thing;
            }
        }

        private CompEntityHolder SourceHolder
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompEntityHolder>();
            }
        }

        private CompEntityHolder DestHolder
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing.TryGetComp<CompEntityHolder>();
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Takee.Map, this.Takee, this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.SourceHolder.parent.Map, this.SourceHolder.parent, this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.DestHolder.parent.Map, this.DestHolder.parent, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.C);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => !this.DestHolder.Available);
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(SourceHolderIndex)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_General.Do(delegate
            {
                CompHoldingPlatformTarget compHoldingPlatformTarget = this.Takee.TryGetComp<CompHoldingPlatformTarget>();
                if (compHoldingPlatformTarget != null)
                {
                    compHoldingPlatformTarget.Notify_ReleasedFromPlatform();
                }
                this.SourceHolder.EjectContents();
            }).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.C, false, false, false, true, false);
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil in this.GotoTargetMap(DestHolderIndex)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch, false);
            foreach (Toil toil in JobDriver_CarryToEntityHolder.ChainTakeeToPlatformToils(this.pawn, this.Takee, this.DestHolder, TargetIndex.B))
            {
                yield return toil;
            }
        }

        private const TargetIndex SourceHolderIndex = TargetIndex.A;

        private const TargetIndex DestHolderIndex = TargetIndex.B;

        private const TargetIndex TakeeIndex = TargetIndex.C;
    }
}
