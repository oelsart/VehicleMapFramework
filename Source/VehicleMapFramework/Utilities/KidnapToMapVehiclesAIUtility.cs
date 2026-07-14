using System.Collections.Generic;
using System.Linq;
using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class KidnapToMapVehiclesAIUtility
{
  public static bool TryFindGoodKidnapVictim(Pawn kidnapper, float maxDist, out Pawn victim, out TargetInfo to, List<Thing> disallowed = null)
  {
    if (!kidnapper.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
    {
      victim = null;
      to = TargetInfo.Invalid;
      return false;
    }

    Patch_GenClosest_ClosestThingReachable.forceCrossMap = true;
    victim = (Pawn)GenClosest.ClosestThingReachable(kidnapper.Position, kidnapper.Map,
      ThingRequest.ForGroup(ThingRequestGroup.Pawn), PathEndMode.OnCell,
      TraverseParms.For(kidnapper, Danger.Some, TraverseMode.NoPassClosedDoors), maxDist, Validator);
    Patch_GenClosest_ClosestThingReachable.forceCrossMap = false;
    if (victim is null)
    {
      to = TargetInfo.Invalid;
      return false;
    }

    if (TryFindPlaceSpot(kidnapper, victim, maxDist, out to))
    {
      kidnapper.TargetInfo = to;
      return true;
    }

    return false;

    bool Validator(Thing t)
    {
      if (t is not Pawn pawn)
        return false;

      if (pawn.Map.ParentFaction == kidnapper.Faction)
        return false;
      
      if (!pawn.RaceProps.Humanlike)
        return false;

      if (!pawn.Downed)
        return false;

      if (pawn.Faction != Faction.OfPlayer)
        return false;

      if (!pawn.Faction.HostileTo(kidnapper.Faction))
        return false;

      if (!kidnapper.CanReserve(pawn))
        return false;

      if (disallowed != null && disallowed.Contains(pawn))
        return false;

      return !ModsConfig.AnomalyActive || !pawn.IsSubhuman;
    }
  }

  public static bool TryFindPlaceSpot(Pawn kidnapper, Thing t, float maxDist, out TargetInfo spot)
  {
    var positionOnBaseMap = t.PositionOnBaseMap;
    var vehicles = VehiclePawnWithMapCache.AllVehiclesOn(kidnapper.GroundMap)
      .Where(v => v.Faction == kidnapper.Faction)
      .OrderBy(v => (positionOnBaseMap - v.Position).LengthHorizontalSquared);
    foreach (var vehicle in vehicles)
    {
      var cell = positionOnBaseMap.ToVehicleMapCoord(vehicle);
      var rect = CellRect.SingleCell(cell).ExpandedBy((int)maxDist).Encapsulate(vehicle.ValidMapRect);
      if (rect.IsEmpty) continue;

      var traverseParms = TraverseParms.For(kidnapper);
      if (CrossMapReachabilityUtility.CanReachToMap(t.Position, t.Map, traverseParms, vehicle.VehicleMap) &&
          CellFinder.TryFindRandomCellInsideWith(rect, Validator, out var dest))
      {
        spot = new TargetInfo(dest, vehicle.VehicleMap);
        return true;
      }
      continue;
  
      bool Validator(IntVec3 c)
      {
        var map = vehicle.VehicleMap;
        if (c.IsForbidden(kidnapper, map))
          return false;

        if (c.GetTerrain(map).passability == Traversability.Impassable)
          return false;
        
        if (!StoreAcrossMapsUtility.NoStorageBlockersIn(c, map, t))
          return false;
        
        if (!kidnapper.CanReserveNew(c, map))
          return false;
        
        if (c.ContainsStaticFire(map))
          return false;
        
        var thingList = c.GetThingList(map);
        if (thingList.Any(t1 => t1 is IConstructible && GenConstruct.BlocksConstruction(t1, t)))
          return false;
        
        return CrossMapReachabilityUtility.CanReach(t.Map, t.Position, c, PathEndMode.ClosestTouch, TraverseParms.For(kidnapper), map);
      }
    }

    spot = TargetInfo.Invalid;
    return false;
  }
}