using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobGiver_HaulAcrossMaps : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            TargetInfo exitSpot = TargetInfo.Invalid;
            TargetInfo enterSpot = TargetInfo.Invalid;
            bool validator(Thing t)
            {
                return !t.IsForbidden(pawn) &&
                (!t.IsOnVehicleMapOf(out var vehicle) || vehicle.AllowsHaulOut) &&
                HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, t, false, out exitSpot, out enterSpot) &&
                pawn.carryTracker.MaxStackSpaceEver(t.def) > 0 &&
                StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(t, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(t), pawn.Faction, out IntVec3 intVec, true, out _, out _, out _);
            }
            var baseMap = pawn.BaseMap();
            var searchSet = baseMap.listerHaulables.ThingsPotentiallyNeedingHauling()
                .Concat(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany(v => v.interiorMap.listerHaulables.ThingsPotentiallyNeedingHauling()));
            Thing thing = GenClosestOnVehicle.ClosestThing_Global_Reachable(pawn.PositionOnBaseMap(), pawn.Map, searchSet, PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, validator, null);
            if (thing != null)
            {
                return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, thing, exitSpot, enterSpot);
            }
            return null;
        }
    }
}