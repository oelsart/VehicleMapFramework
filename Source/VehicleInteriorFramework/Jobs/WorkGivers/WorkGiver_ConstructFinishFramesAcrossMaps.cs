using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_ConstructFinishFramesAcrossMaps : WorkGiver_ConstructFinishFrames, IWorkGiverAcrossMaps
    {
        public bool NeedVirtualMapTransfer => false;

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
                return GenConstructOnVehicle.HandleBlockingThingJob(frame, pawn, forced);
            }
            if (!GenConstruct.CanConstruct(frame, pawn, true, forced, null))
            {
                return null;
            }
            if (!pawn.CanReach(frame, PathEndMode.Touch, forced ? Danger.Deadly : pawn.NormalMaxDanger(), false, false, TraverseMode.ByPawn, frame.Map, out var exitSpot, out var enterSpot))
            {
                return null;
            }
            if (exitSpot.Map != null || enterSpot.Map != null)
            {
                return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.FinishFrame, frame));
            }
            else
            {
                return JobMaker.MakeJob(JobDefOf.FinishFrame, frame);
            }
        }
    }
}
