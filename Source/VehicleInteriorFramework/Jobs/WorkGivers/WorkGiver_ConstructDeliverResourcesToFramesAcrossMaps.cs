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
                var job = GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
                if (job != null && pawn.Map != frame.Map && pawn.CanReach(frame, this.PathEndMode, this.MaxPathDanger(pawn), false, false, TraverseMode.ByPawn, frame.Map, out var exitSpot, out var enterSpot))
                {
                    return job;
                }
                return GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
            }
            if (!GenConstructOnVehicle.CanConstruct(frame, pawn, this.def.workType, forced, VIF_DefOf.VIF_HaulToCellAcrossMaps))
            {
                return null;
            }
            return base.ResourceDeliverJobFor(pawn, frame, true, forced);
        }
    }
}
