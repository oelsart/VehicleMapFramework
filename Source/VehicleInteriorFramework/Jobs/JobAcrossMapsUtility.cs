using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class JobAcrossMapsUtility
    {
        public static void TryTakeGotoDestMapJob(Pawn pawn, LocalTargetInfo? exitSpot1 = null, LocalTargetInfo? enterSpot1 = null)
        {
            Job preJob = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps);
            var driver = preJob.GetCachedDriver(pawn) as JobDriverAcrossMaps;
            driver.SetSpots(exitSpot1, enterSpot1);
            pawn.jobs.TryTakeOrderedJob(preJob, new JobTag?(JobTag.Misc), false);
        }
    }
}
