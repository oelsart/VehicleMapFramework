using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class CrossMapRCellFinder
{
  public static IntVec3 BestOrderedGotoDestNear(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, bool reachable, Map map)
  {
    if (map is null)
    {
      return IntVec3.Invalid;
    }
    if (IsGoodDest(root))
    {
      return root;
    }
    var num = 1;
    IntVec3 result = default;
    var num2 = -1000f;
    var flag = false;
    var num3 = GenRadial.NumCellsInRadius(30f);
    do
    {
      var intVec = root + GenRadial.RadialPattern[num];
      if (IsGoodDest(intVec))
      {
        var num4 = CoverUtility.TotalSurroundingCoverScore(intVec, map);
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
    } while (num < num3);
    return searcher.Position;

    bool IsGoodDest(IntVec3 c)
    {
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
      if (reachable && !searcher.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map))
      {
        return false;
      }
      var thingList = c.GetThingList(map);
      for (var i = 0; i < thingList.Count; i++)
      {
        if (thingList[i] is Pawn pawn && pawn != searcher && pawn.RaceProps.Humanlike && (searcher.Faction == Faction.OfPlayer && pawn.Faction == searcher.Faction || searcher.Faction != Faction.OfPlayer && pawn.Faction != Faction.OfPlayer))
        {
          return false;
        }
      }
      return true;
    }
  }

  public static IntVec3 GoodDestNearFromTo(IntVec3 from, IntVec3 to, Pawn searcher, Map map, Predicate<IntVec3> cellValidator = null, bool reachable = true, bool reserve = true, float radius = 30f)
  {
    if (map is null)
      return IntVec3.Invalid;
    if (IsGoodDest(to))
      return to;
    var num = 1;
    var num3 = GenRadial.NumCellsInRadius(radius);
    do
    {
      var intVec = to + GenRadial.RadialPattern[num];
      if (IsGoodDest(intVec))
        return intVec;
      num++;
    } while (num < num3);
    return IntVec3.Invalid;

    bool IsGoodDest(IntVec3 c)
    {
      if (!IsGoodDestinationFor(c, searcher, map, false))
      {
        return false;
      }
      if (cellValidator != null && !cellValidator(c))
      {
        return false;
      }
      if (reserve && !map.pawnDestinationReservationManager.CanReserve(c, searcher, true))
      {
        return false;
      }
      if (reachable && !map.reachability.CanReach(from, c, PathEndMode.OnCell, TraverseMode.ByPawn, Danger.Deadly))
      {
        return false;
      }
      var thingList = c.GetThingList(map);
      for (var i = 0; i < thingList.Count; i++)
      {
        if (thingList[i] is Pawn pawn && pawn != searcher && pawn.RaceProps.Humanlike && (searcher.Faction == Faction.OfPlayer && pawn.Faction == searcher.Faction || searcher.Faction != Faction.OfPlayer && pawn.Faction != Faction.OfPlayer))
        {
          return false;
        }
      }
      return true;
    }
  }

  private static bool IsGoodDestination(IntVec3 c, Map map, bool careAboutDanger)
  {
    return c.Standable(map) && (!careAboutDanger || !c.GetTerrain(map).dangerous);
  }

  private static bool IsGoodDestinationFor(IntVec3 c, Pawn pawn, Map map, bool careAboutDanger)
  {
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
      var door = c.GetDoor(map);
      if (door == null || !door.CanPhysicallyPass(pawn))
      {
        return false;
      }
    }
    return !c.IsForbidden(pawn) && (!careAboutDanger || c.GetDangerFor(pawn, map) != Danger.Deadly) && (!careAboutDanger || !PawnUtility.KnownDangerAt(c, map, pawn)) && (!careAboutDanger || !VacuumConcernTo(c));

    bool VacuumConcernTo(IntVec3 cell)
    {
      return pawn.ConcernedByVacuum && cell.GetVacuum(map) >= 0.5f;
    }
  }

  public static bool TryFindGoodAdjacentSpotToTouch(Pawn toucher, Thing touchee, out IntVec3 result)
  {
    var intVec = IntVec3.Invalid;
    var num = int.MaxValue;
    var map = touchee.MapHeld ?? toucher.Map;
    var positionOnThingMap = toucher.PositionOnAnotherThingMap(touchee);
    foreach (var item in GenAdj.CellsAdjacent8Way(touchee))
    {
      if (IsGoodDestinationFor(item, toucher, map, true) && toucher.CanReach(item, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map) && ReachabilityImmediate.CanReachImmediate(item, touchee, toucher.Map, PathEndMode.Touch, toucher))
      {
        if (positionOnThingMap == item && map == toucher.Map)
        {
          intVec = item;
          break;
        }
        var num2 = positionOnThingMap.DistanceToSquared(item);
        if (num2 < num || intVec.GetTerrain(map).avoidWander && !item.GetTerrain(map).avoidWander || intVec.GetFirstThing<Building_Trap>(map) != null && item.GetFirstThing<Building_Trap>(map) == null)
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
    foreach (var item2 in GenAdj.CellsAdjacent8Way(touchee).InRandomOrder())
    {
      if (item2.WalkableBy(map, toucher) && toucher.CanReach(item2,
            PathEndMode.OnCell,
            Danger.Deadly,
            false,
            false,
            TraverseMode.ByPawn,
            map))
      {
        result = item2;
        return true;
      }
    }
    result = touchee.Position;
    return false;
  }
}
