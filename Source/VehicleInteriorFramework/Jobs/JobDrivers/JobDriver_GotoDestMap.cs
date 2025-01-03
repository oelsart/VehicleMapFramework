﻿using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_GotoDestMap : JobDriverAcrossMaps
    {
        protected override string ReportStringProcessed(string str)
        {
            return this.nextJob.GetReport(this.pawn);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var driver = nextJob.GetCachedDriver(this.pawn);
            var map = this.pawn.Map;
            var map2 = this.TargetAMap;
            if (map != map2)
            {
                this.pawn.VirtualMapTransfer(map2);
            }
            var result = nextJob.TryMakePreToilReservations(this.pawn, false);
            if (map != map2)
            {
                this.pawn.VirtualMapTransfer(map);
            }
            return result;
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
                    this.pawn.jobs.TryTakeOrderedJob(this.nextJob, JobTag.Misc, false);
                };
                yield return toil;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref this.nextJob, "nextJob");
        }

        public Job nextJob;
    }
}
