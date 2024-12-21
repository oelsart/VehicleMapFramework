using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_ConstructDeliverResourcesToFramesAcrossMaps : WorkGiver_ConstructDeliverResourcesAcrossMaps
    {
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
            if (GenConstruct.FirstBlockingThing(frame, pawn) != null)
            {
                return GenConstructOnVehicle.HandleBlockingThingJob(frame, pawn, forced);
            }
            if (!GenConstructOnVehicle.CanConstruct(frame, pawn, this.def.workType, forced, VIF_DefOf.VIF_HaulToCellAcrossMaps, out var exitSpot, out var enterSpot))
            {
                return null;
            }
            var job = base.ResourceDeliverJobFor(pawn, frame, true, forced);
            if (job != null)
            {
                return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job);
            }
            return null;
        }
    }
}
