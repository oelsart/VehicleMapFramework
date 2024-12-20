using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_TakeAndExitMapAcrossMaps : JobDriverAcrossMaps
    {
        protected Thing Item
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Item.Map, this.Item, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil3 in this.GotoTargetMap(ItemInd)) yield return toil3;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil3 in this.GotoTargetMap(ExitCellInd)) yield return toil3;
            }
            Toil toil = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            toil.AddPreTickAction(delegate
            {
                if (base.Map.exitMapGrid.IsExitCell(this.pawn.Position))
                {
                    this.pawn.ExitMap(true, CellRect.WholeMap(base.Map).GetClosestEdge(this.pawn.Position));
                }
            });
            toil.FailOn(() => this.job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(this.pawn));
            yield return toil;
            Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
            toil2.initAction = delegate ()
            {
                if (this.pawn.Position.OnEdge(this.pawn.Map) || this.pawn.Map.exitMapGrid.IsExitCell(this.pawn.Position))
                {
                    this.pawn.ExitMap(true, CellRect.WholeMap(base.Map).GetClosestEdge(this.pawn.Position));
                }
            };
            toil2.FailOn(() => this.job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(this.pawn));
            toil2.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil2;
            yield break;
        }

        private const TargetIndex ItemInd = TargetIndex.A;

        private const TargetIndex ExitCellInd = TargetIndex.B;
    }
}
