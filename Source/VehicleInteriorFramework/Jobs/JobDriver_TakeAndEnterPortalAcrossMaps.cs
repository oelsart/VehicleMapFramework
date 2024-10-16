using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_TakeAndEnterPortalAcrossMaps : JobDriver_EnterPortalAcrossMaps
    {
        private Thing ThingToTake
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        private Pawn PawnToTake
        {
            get
            {
                return this.ThingToTake as Pawn;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.ThingToTake.Map, this.ThingToTake, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => this.PawnToTake != null && !this.PawnToTake.Downed && this.PawnToTake.Awake());
            if (this.ShouldEnterTargetBMap)
            {
                foreach(var toil in this.GotoTargetMap(ThingInd)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch, false).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Construct.UninstallIfMinifiable(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true, false);
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }
        }

        private const TargetIndex ThingInd = TargetIndex.B;
    }
}