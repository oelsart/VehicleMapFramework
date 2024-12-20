using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_AttackMeleeAcrossMaps : JobDriverAcrossMaps
    {
        private Job AttackMeleeJob
        {
            get
            {
                Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, this.job.targetA);
                job.expiryInterval = new IntRange(360, 480).RandomInRange;
                job.checkOverrideOnExpire = true;
                job.expireRequiresEnemiesNearby = true;
                return job;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.job.targetB.HasThing)
            {
                this.job.targetB.Thing.Map.pawnDestinationReservationManager.Reserve(this.pawn, this.job, this.job.targetC.Cell);
            }
            else this.pawn.Map.pawnDestinationReservationManager.Reserve(this.pawn, this.job, this.job.targetC.Cell);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A))
                {
                    yield return toil;
                }
            }
            if (job.targetA.IsValid)
            {
                var toil = ToilMaker.MakeToil("MakeJobAttackMelee");
                toil.defaultCompleteMode = ToilCompleteMode.Instant;
                var nextJob = this.AttackMeleeJob;
                toil.initAction = () =>
                {
                    this.pawn.jobs.TryTakeOrderedJob(nextJob, JobTag.Misc, false);
                };
                yield return toil;
            }
        }
    }
}
