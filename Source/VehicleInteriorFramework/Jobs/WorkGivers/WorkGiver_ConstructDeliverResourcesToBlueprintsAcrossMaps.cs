using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_ConstructDeliverResourcesToBlueprintsAcrossMaps : WorkGiver_ConstructDeliverResourcesAcrossMaps
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Blueprint);
            }
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction)
            {
                return null;
            }
            if (!(t is Blueprint blueprint) || (blueprint.def.entityDefToBuild is ThingDef thingDef && thingDef.plant != null))
            {
                return null;
            }
            if (GenConstruct.FirstBlockingThing(blueprint, pawn) != null)
            {
                return GenConstructOnVehicle.HandleBlockingThingJob(blueprint, pawn, forced);
            }
            if (!GenConstructOnVehicle.CanConstruct(blueprint, pawn, this.def.workType, forced, VMF_DefOf.VMF_HaulToCellAcrossMaps, out var exitSpot, out var enterSpot))
            {
                return null;
            }
            if (this.def.workType != WorkTypeDefOf.Construction && WorkGiver_ConstructDeliverResourcesAcrossMaps.ShouldRemoveExistingFloorFirst(pawn, blueprint))
            {
                return null;
            }
            Job job2 = base.RemoveExistingFloorJob(pawn, blueprint);
            if (job2 != null)
            {
                return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job2);
            }
            Job job3 = base.ResourceDeliverJobFor(pawn, blueprint, true, forced);
            if (job3 != null)
            {
                return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job3);
            }
            if (this.def.workType != WorkTypeDefOf.Hauling)
            {
                Job job4 = this.NoCostFrameMakeJobFor(blueprint);
                if (job4 != null)
                {
                    return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job4);
                }
            }
            return null;
        }

        private Job NoCostFrameMakeJobFor(IConstructible c)
        {
            if (c is Blueprint_Install)
            {
                return null;
            }
            if (c is Blueprint && c.TotalMaterialCost().Count == 0)
            {
                Job job = JobMaker.MakeJob(JobDefOf.PlaceNoCostFrame);
                job.targetA = (Thing)c;
                return job;
            }
            return null;
        }
    }
}
