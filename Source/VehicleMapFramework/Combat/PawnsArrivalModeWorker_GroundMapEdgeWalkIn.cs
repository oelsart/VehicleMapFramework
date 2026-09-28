using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class PawnsArrivalModeWorker_GroundMapEdgeWalkIn : PawnsArrivalModeWorker
{
  public override bool CanUseOnMap(Map map)
  {
    return map.IsVehicleMap && base.CanUseOnMap(map);
  }

  public override void Arrive(List<Pawn> pawns, IncidentParms parms)
  {
    var target = (Map)parms.target;
    for (var i = 0; i < pawns.Count; ++i)
    {
      var loc = CellFinder.RandomClosewalkCellNear(parms.spawnCenter, target, 8);
      GenSpawn.Spawn(pawns[i], loc, target, parms.spawnRotation);
    }
  }

  public override bool TryResolveRaidSpawnCenter(IncidentParms parms)
  {
    var map = (Map)parms.target;
    if (!map.IsVehicleMapOf(out var vehicle))
      return false;

    if (vehicle.Spawned)
    {
      var groundMap = vehicle.Map;
      if (parms.attackTargets is { Count: > 0 } &&
          !RCellFinder.TryFindEdgeCellFromThingAvoidingColony(parms.attackTargets[0], groundMap,
            (from, to) => PredicateHard(from, to, groundMap),
            out parms.spawnCenter))
      {
        CellFinder.TryFindRandomEdgeCellWith(c => PredicateEasy(c, groundMap), groundMap,
          CellFinder.EdgeRoadChance_Hostile, out parms.spawnCenter);
      }

      if (!parms.spawnCenter.IsValid)
      {
        RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, groundMap, CellFinder.EdgeRoadChance_Hostile);
      }

      if (parms.spawnCenter.IsValid)
      {
        parms.target = groundMap;
        parms.spawnRotation = Rot4.FromAngleFlat((groundMap.Center - parms.spawnCenter).AngleFlat);
        return true;
      }
    }
    
    if (!VehicleMapCellFinder.TryFindRandomEdgeCellWith(c => PredicateEasy(c, map), vehicle,
          out parms.spawnCenter))
    {
      if (!VehicleMapCellFinder.TryFindRandomEdgeCellWith(null, vehicle, out parms.spawnCenter))
        return false;
    }
    parms.spawnRotation = Rot4.FromAngleFlat((map.Center - parms.spawnCenter).AngleFlat);
    return true;
    
    static bool PredicateHard(IntVec3 from, IntVec3 to, Map map)
    {
      if ((map.TileInfo.AllowRoofedEdgeWalkIn || !map.roofGrid.Roofed(from)) && from.Walkable(map))
      {
        return map.reachability.CanReach(from, to, PathEndMode.OnCell, TraverseMode.NoPassClosedDoors, Danger.Some);
      }
      return false;
    }

    static bool PredicateEasy(IntVec3 c, Map map)
    {
      return (map.TileInfo.AllowRoofedEdgeWalkIn || !map.roofGrid.Roofed(c)) && c.Walkable(map);
    }
  }
}