using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_GotoAcrossMaps : JobDriverAcrossMaps
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return !this.job.targetA.IsValid || this.pawn.Reserve(this.DestMap, this.job.targetA.Cell, this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.B)) yield return toil;
            }
            if (job.targetA.IsValid)
            {
                var toil = Toils_Goto.Goto(TargetIndex.A, PathEndMode.OnCell);
                yield return toil;
            }
        }
    }
}
