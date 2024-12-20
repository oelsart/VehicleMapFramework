using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class JobAcrossMapsUtility
    {
        public static Job GotoDestMapJob(Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null)
        {
            return JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot1, enterSpot1);
        }

        public static void TryTakeGotoDestMapJob(Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null)
        {
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot1, enterSpot1), new JobTag?(JobTag.Misc), false);
        }

        public static Job SetSpotsToJobAcrossMaps(this Job job, Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null)
        {
            var driver = job.GetCachedDriver(pawn) as JobDriverAcrossMaps;
            driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
            return job;
        }
    }
}
