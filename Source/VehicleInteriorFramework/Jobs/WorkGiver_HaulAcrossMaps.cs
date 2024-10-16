using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_HaulAcrossMaps : WorkGiver_Haul
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var baseMap = pawn.BaseMapOfThing();
            return baseMap.listerHaulables.ThingsPotentiallyNeedingHauling()
                .Concat(VehiclePawnWithMapCache.allVehicles[baseMap].Where(v => v.AllowsAutoHaul).SelectMany(v => v.interiorMap.listerHaulables.ThingsPotentiallyNeedingHauling()));
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is Corpse)
            {
                return null;
            }
            var exitSpot = LocalTargetInfo.Invalid;
            var enterSpot = LocalTargetInfo.Invalid;
            if (!HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced, out exitSpot, out enterSpot))
            {
                return null;
            }
            return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, t, exitSpot, enterSpot);
        }
    }
}
