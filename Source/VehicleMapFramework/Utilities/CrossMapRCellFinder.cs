using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class CrossMapRCellFinder
{
    public static IntVec3 BestOrderedGotoDestNear(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, bool reachable, Map map, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        bool IsGoodDest(IntVec3 c, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            if (!IsGoodDestinationFor(c, searcher, map, false))
            {
                return false;
            }
            if (cellValidator != null && !cellValidator(c))
            {
                return false;
            }
            if (!map.pawnDestinationReservationManager.CanReserve(c, searcher, true))
            {
                return false;
            }
            if (reachable && !searcher.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out exitSpot, out enterSpot))
            {
                return false;
            }
            List<Thing> thingList = c.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (thingList[i] is Pawn pawn && pawn != searcher && pawn.RaceProps.Humanlike && ((searcher.Faction == Faction.OfPlayer && pawn.Faction == searcher.Faction) || (searcher.Faction != Faction.OfPlayer && pawn.Faction != Faction.OfPlayer)))
                {
                    return false;
                }
            }
            return true;
        }

        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (map is null)
        {
            return IntVec3.Invalid;
        }
        if (IsGoodDest(root, out exitSpot, out enterSpot))
        {
            return root;
        }
        int num = 1;
        IntVec3 result = default;
        float num2 = -1000f;
        bool flag = false;
        int num3 = GenRadial.NumCellsInRadius(30f);
        do
        {
            IntVec3 intVec = root + GenRadial.RadialPattern[num];
            if (IsGoodDest(intVec, out exitSpot, out enterSpot))
            {
                float num4 = CoverUtility.TotalSurroundingCoverScore(intVec, map);
                if (num4 > num2)
                {
                    num2 = num4;
                    result = intVec;
                    flag = true;
                }
            }
            if (num >= 8 && flag)
            {
                return result;
            }
            num++;
        }
        while (num < num3);
        return searcher.Position;
    }

    private static bool IsGoodDestination(IntVec3 c, Map map, bool careAboutDanger)
    {
        return c.Standable(map) && (!careAboutDanger || !c.GetTerrain(map).dangerous);
    }

    private static bool IsGoodDestinationFor(IntVec3 c, Pawn pawn, Map map, bool careAboutDanger)
    {
        bool VacuumConcernTo(IntVec3 cell, Pawn pawn)
        {
            return pawn.ConcernedByVacuum && cell.GetVacuum(map) >= 0.5f;
        }

        if (!IsGoodDestination(c, map, careAboutDanger))
        {
            return false;
        }
        if (!c.WalkableBy(map, pawn))
        {
            return false;
        }
        if (!c.Standable(map))
        {
            Building_Door door = c.GetDoor(map);
            if (door == null || !door.CanPhysicallyPass(pawn))
            {
                return false;
            }
        }
        return !c.IsForbidden(pawn) && (!careAboutDanger || c.GetDangerFor(pawn, map) != Danger.Deadly) && (!careAboutDanger || !PawnUtility.KnownDangerAt(c, map, pawn)) && (!careAboutDanger || !VacuumConcernTo(c, pawn));
    }

    public static bool TryFindGoodAdjacentSpotToTouch(Pawn toucher, Thing touchee, out IntVec3 result)
    {
        IntVec3 intVec = IntVec3.Invalid;
        int num = int.MaxValue;
        var map = touchee.MapHeld ?? toucher.Map;
        var positionOnThingMap = toucher.PositionOnAnotherThingMap(touchee);
        foreach (IntVec3 item in GenAdj.CellsAdjacent8Way(touchee))
        {
            if (IsGoodDestinationFor(item, toucher, map, careAboutDanger: true) && toucher.CanReach(item, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out _, out _) && ReachabilityImmediate.CanReachImmediate(item, touchee, toucher.Map, PathEndMode.Touch, toucher))
            {
                if (positionOnThingMap == item && map == toucher.Map)
                {
                    intVec = item;
                    break;
                }
                int num2 = positionOnThingMap.DistanceToSquared(item);
                if (num2 < num || (intVec.GetTerrain(map).avoidWander && !item.GetTerrain(map).avoidWander) || (intVec.GetFirstThing<Building_Trap>(map) != null && item.GetFirstThing<Building_Trap>(map) == null))
                {
                    num = num2;
                    intVec = item;
                }
            }
        }
        if (intVec.IsValid)
        {
            result = intVec;
            return true;
        }
        foreach (IntVec3 item2 in GenAdj.CellsAdjacent8Way(touchee).InRandomOrder())
        {
            if (item2.WalkableBy(map, toucher) && toucher.CanReach(item2, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out _, out _))
            {
                result = item2;
                return true;
            }
        }
        result = touchee.Position;
        return false;
    }
}
