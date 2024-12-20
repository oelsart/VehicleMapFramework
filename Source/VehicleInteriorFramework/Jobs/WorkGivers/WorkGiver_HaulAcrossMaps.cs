using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_HaulAcrossMaps : WorkGiver_Haul
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is Corpse)
            {
                return null;
            }

            if (!HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced, out TargetInfo exitSpot, out TargetInfo enterSpot))
            {
                return null;
            }
            return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, t, exitSpot, enterSpot);
        }
    }
}
