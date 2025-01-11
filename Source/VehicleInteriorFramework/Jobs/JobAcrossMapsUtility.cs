using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class JobAcrossMapsUtility
    {
        public static Job GotoDestMapJob(Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, Job nextJob = null)
        {
            if (((enterSpot1.HasValue && enterSpot1.Value.IsValid) || (exitSpot1.HasValue && exitSpot1.Value.IsValid)) && !(nextJob.GetCachedDriver(pawn) is JobDriverAcrossMaps))
            {
                return JobMaker.MakeJob(VIF_DefOf.VIF_GotoDestMap).SetSpotsAndNextJob(pawn, exitSpot1, enterSpot1, nextJob: nextJob);
            }
            return nextJob;
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

        public static Job SetSpotsAndNextJob(this Job job, Pawn pawn, TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null, Job nextJob = null)
        {
            var driver = job.GetCachedDriver(pawn) as JobDriver_GotoDestMap;
            driver.SetSpots(exitSpot1, enterSpot1, exitSpot2, enterSpot2);
            driver.nextJob = nextJob;
            return job;
        }

        public static bool PawnDeterminingJob(this Pawn pawn)
        {
            return pawn.jobs.DeterminingNextJob || FloatMenuMakerMap.makingFor == pawn || Find.Selector.SingleSelectedObject == pawn;
        }
    }
}
