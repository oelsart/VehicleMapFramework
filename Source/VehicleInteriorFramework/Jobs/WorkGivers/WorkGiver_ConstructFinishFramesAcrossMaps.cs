using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_ConstructFinishFramesAcrossMaps : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.BuildingFrame);
            }
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction)
            {
                return null;
            }
            if (!(t is Frame frame))
            {
                return null;
            }
            if (!frame.IsCompleted())
            {
                return null;
            }
            if (GenConstruct.FirstBlockingThing(frame, pawn) != null)
            {
                var job = GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
                if (job != null && pawn.Map != frame.Map && pawn.CanReach(frame, this.PathEndMode, this.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, frame.Map, out var exitSpot, out var enterSpot))
                {
                    return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot);
                }
                return job;
            }
            if (!GenConstructOnVehicle.CanConstruct(frame, pawn, true, forced, null))
            {
                return null;
            }
            var job2 = JobMaker.MakeJob(JobDefOf.FinishFrame, frame);
            if (job2 != null && pawn.Map != frame.Map && pawn.CanReach(frame, this.PathEndMode, this.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, frame.Map, out var exitSpot2, out var enterSpot2))
            {
                return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot2, enterSpot2);
            }
            return job2;
        }
    }
}
