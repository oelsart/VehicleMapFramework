using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class JobAcrossMapsUtility
    {
        public static void TryTakeGotoDestMapJob(Pawn pawn, LocalTargetInfo? exitSpot1 = null, LocalTargetInfo? enterSpot1 = null)
        {
            Job preJob = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps);
            preJob.SetSpotsToJobAcrossMaps(pawn, exitSpot1, enterSpot1);
            pawn.jobs.TryTakeOrderedJob(preJob, new JobTag?(JobTag.Misc), false);
        }

        public static void SetSpotsToJobAcrossMaps(this Job job, Pawn pawn, LocalTargetInfo? exitSpot1 = null, LocalTargetInfo? enterSpot1 = null, LocalTargetInfo? exitSpot2 = null, LocalTargetInfo? enterSpot2 = null)
        {
            var driver = job.GetCachedDriver(pawn) as JobDriverAcrossMaps;
            driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
        }
    }
}
