using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors;

public class WorkGiver_HaulAcrossMaps : WorkGiver_Haul, IWorkGiverAcrossMaps
{
    public bool NeedVirtualMapTransfer => false;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        if (!pawn.Map.BaseMapAndVehicleMaps().Except(pawn.Map).Any()) return true;
        return base.ShouldSkip(pawn, forced);
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.BaseMapAndVehicleMaps().SelectMany(m => m.listerHaulables.ThingsPotentiallyNeedingHauling());
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced, out TargetInfo exitSpot, out TargetInfo enterSpot))
        {
            return null;
        }
        return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, t, forced, exitSpot, enterSpot);
    }
}