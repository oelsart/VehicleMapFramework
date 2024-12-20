using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_CarryToEntityHolderAcrossMaps : JobDriverAcrossMaps
    {
        private Thing Takee
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        private CompEntityHolder DestHolder
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompEntityHolder>();
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Takee, this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.DestHolder.parent, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !this.DestHolder.Available);
            this.FailOn(delegate ()
            {
                Pawn pawn;
                if ((pawn = (this.Takee as Pawn)) != null)
                {
                    CompActivity comp = pawn.GetComp<CompActivity>();
                    return comp != null && !comp.IsDormant;
                }
                return false;
            });
            this.FailOn(() => this.Takee.TryGetComp<CompHoldingPlatformTarget>().EntityHolder != this.DestHolder);
            if (this.pawn.carryTracker.CarriedThing != this.Takee)
            {
                if (this.ShouldEnterTargetBMap)
                {
                    foreach (var toil in this.GotoTargetMap(TakeeIndex)) yield return toil;
                }
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell, false);
            }
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true, false);
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(DestHolderIndex)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false);
            foreach (Toil toil in JobDriver_CarryToEntityHolder.ChainTakeeToPlatformToils(this.pawn, this.Takee, this.DestHolder, TargetIndex.A))
            {
                yield return toil;
            }
            yield return Toils_General.Do(delegate
            {
                Pawn pawn;
                if ((pawn = (this.Takee as Pawn)) != null && (!pawn.RaceProps.Humanlike || pawn.IsMutant))
                {
                    TaleRecorder.RecordTale(TaleDefOf.Captured, new object[]
                    {
                        this.pawn,
                        pawn
                    });
                }
            });
        }

        private const TargetIndex DestHolderIndex = TargetIndex.A;

        private const TargetIndex TakeeIndex = TargetIndex.B;

        private const int EnterDelayTicks = 300;
    }
}
