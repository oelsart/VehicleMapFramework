using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_CarryToEntityHolderAlreadyHoldingAcrossMaps : JobDriverAcrossMaps
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
            return this.pawn.Reserve(this.Takee.Map, this.DestHolder.parent, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !this.DestHolder.Available);
            var startCarry = Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true, false);
            yield return startCarry;
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil in this.GotoTargetMap(TakeeIndex)) yield return toil;
            }
            foreach (Toil toil in JobDriver_CarryToEntityHolder.ChainTakeeToPlatformToils(this.pawn, this.Takee, this.DestHolder, TargetIndex.A))
            {
                yield return toil;
            }
            yield break;
        }

        private const TargetIndex DestIndex = TargetIndex.A;

        private const TargetIndex TakeeIndex = TargetIndex.B;
    }
}
