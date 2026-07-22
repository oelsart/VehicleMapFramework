using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class StealToMapVehiclesAIUtility
{
  private const float MinMarketValueToTake = 320f;

  private static readonly List<Thing> tmpToSteal = [];
  
  public static bool TryFindBestItemToSteal(Pawn thief, float maxDist, out Thing item, out TargetInfo to, List<Thing> disallowed = null)
  {
    if (!thief.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
    {
      item = null;
      to = TargetInfo.Invalid;
      return false;
    }

    item = GenClosestCrossMap.ClosestThing_Regionwise_ReachablePrioritized(thief.Position, thief.Map,
      ThingRequest.ForGroup(ThingRequestGroup.HaulableEverOrMinifiable), PathEndMode.ClosestTouch,
      TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Some), maxDist, Predicate, StealAIUtility.GetValue,
      15, 15);
    if (item is not null && StealAIUtility.GetValue(item) < MinMarketValueToTake)
    {
      item = null;
    }
    if (item is null)
    {
      to = TargetInfo.Invalid;
      return false;
    }

    if (KidnapToMapVehiclesAIUtility.TryFindPlaceSpot(thief, item, maxDist, out to))
    {
      thief.TargetInfo = to;
      return true;
    }

    return false;

    bool Predicate(Thing t) => t.Map.ParentFaction != thief.Faction &&
                               thief.CanReserve(t) &&
                               (disallowed == null || !disallowed.Contains(t)) &&
                               t.def.stealable && !t.IsBurning();
  }
  
  public static float TotalMarketValueAround(List<Pawn> pawns)
  {
    var num = 0f;
    tmpToSteal.Clear();
    for (var i = 0; i < pawns.Count; i++)
    {
      if (pawns[i].Spawned &&
          TryFindBestItemToSteal(pawns[i], JobGiver_Steal.ItemsSearchRadiusInitial,
            out var thing, out _, tmpToSteal))
      {
        num += StealAIUtility.GetValue(thing);
        tmpToSteal.Add(thing);
      }
    }
    tmpToSteal.Clear();
    return num;
  }
}