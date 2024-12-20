using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_CarryToCryptosleepCasketAcrossMaps : JobDriverAcrossMaps
    {
        protected Pawn Takee
        {
            get
            {
                return (Pawn)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected Building_CryptosleepCasket DropPod
        {
            get
            {
                return (Building_CryptosleepCasket)this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Takee, this.job, 1, -1, null, errorOnFailed, false) && this.pawn.Reserve(this.DropPod, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnAggroMentalState(TargetIndex.A);
            this.FailOn(() => !this.DropPod.Accepts(this.Takee));
            Toil goToTakee = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell, false).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOn(() => this.DropPod.GetDirectlyHeldThings().Count > 0).FailOn(() => !this.pawn.CanReach(this.Takee, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn)).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            Toil startCarryingTakee = Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            Toil goToThing = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell, false);
            yield return Toils_Jump.JumpIf(goToThing, () => this.pawn.IsCarryingPawn(this.Takee));
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil3 in this.GotoTargetMap(TakeeInd)) yield return toil3;
            }
            yield return goToTakee;
            yield return startCarryingTakee;
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil3 in this.GotoTargetMap(DropPodInd)) yield return toil3;
            }
            yield return goToThing;
            Toil toil = Toils_General.Wait(500, TargetIndex.B);
            toil.FailOnCannotTouch(TargetIndex.B, PathEndMode.InteractionCell);
            toil.WithProgressBarToilDelay(TargetIndex.B, false, -0.5f);
            yield return toil;
            Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
            toil2.initAction = delegate ()
            {
                this.DropPod.TryAcceptThing(this.Takee, true);
            };
            toil2.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil2;
            yield break;
        }

        public override object[] TaleParameters()
        {
            return new object[]
            {
                this.pawn,
                this.Takee
            };
        }

        private const TargetIndex TakeeInd = TargetIndex.A;

        private const TargetIndex DropPodInd = TargetIndex.B;
    }
}
