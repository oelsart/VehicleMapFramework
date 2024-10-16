using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobGiver_HaulAcrossMaps : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            LocalTargetInfo exitSpot = LocalTargetInfo.Invalid;
            LocalTargetInfo enterSpot = LocalTargetInfo.Invalid;
            Predicate<Thing> validator = delegate (Thing t)
            {
                IntVec3 intVec;

                return !t.IsForbidden(pawn) &&
                (!t.IsOnVehicleMapOf(out var vehicle) || vehicle.AllowsAutoHaul) &&
                HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, t, false, out exitSpot, out enterSpot) &&
                pawn.carryTracker.MaxStackSpaceEver(t.def) > 0 &&
                StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(t, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(t), pawn.Faction, out intVec, true, out _, out _);
            };
            var searchSet = pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling();
            Thing thing = GenClosestOnVehicle.ClosestThing_Global_Reachable(pawn.PositionOnBaseMap(), pawn.Map, searchSet, PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, validator, null);
            if (thing != null)
            {
                return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, thing, exitSpot, enterSpot);
            }
            return null;
        }
    }
}
