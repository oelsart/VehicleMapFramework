using HarmonyLib;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_GotoDestMap : JobDriverAcrossMaps
    {
        protected override string ReportStringProcessed(string str)
        {
            return this.nextJob?.GetReport(this.pawn);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var result = nextJob?.TryMakePreToilReservations(this.pawn, false);
            return result ?? true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            if (this.nextJob != null)
            {
                var toil = ToilMaker.MakeToil("TryTakeNextJob");
                toil.defaultCompleteMode = ToilCompleteMode.Instant;
                toil.initAction = () =>
                {
                    this.pawn.jobs.StartJob(this.nextJob, JobCondition.InterruptForced, keepCarryingThingOverride: true);
                };
                yield return toil;
            }
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref this.nextJob, "nextJob");
            base.ExposeData();
        }

        public Job nextJob;
    }
}
