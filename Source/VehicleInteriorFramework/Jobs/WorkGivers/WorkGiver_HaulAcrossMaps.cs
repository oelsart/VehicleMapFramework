using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_HaulAcrossMaps : WorkGiver_Haul
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var baseMap = pawn.BaseMap();
            return baseMap.listerHaulables.ThingsPotentiallyNeedingHauling()
                .Concat(VehiclePawnWithMapCache.allVehicles[baseMap].Where(v => v.AllowsHaulOut).SelectMany(v => v.interiorMap.listerHaulables.ThingsPotentiallyNeedingHauling())).Count() == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var baseMap = pawn.BaseMap();
            return baseMap.listerHaulables.ThingsPotentiallyNeedingHauling()
                .Concat(VehiclePawnWithMapCache.allVehicles[baseMap].Where(v => v.AllowsHaulOut).SelectMany(v => v.interiorMap.listerHaulables.ThingsPotentiallyNeedingHauling()));
        }

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
