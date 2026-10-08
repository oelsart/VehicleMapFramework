using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class PawnsArrivalModeWorker_VehicleMapEdgeWalkIn : PawnsArrivalModeWorker
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
    
    if (!VehicleMapCellFinder.TryFindRandomEdgeCellWith(c => Predicate(c, map), vehicle,
          out parms.spawnCenter))
    {
      if (!VehicleMapCellFinder.TryFindRandomEdgeCellWith(null, vehicle, out parms.spawnCenter))
        return false;
    }

    return true;
    
    static bool Predicate(IntVec3 c, Map map)
    {
      return (map.TileInfo.AllowRoofedEdgeWalkIn || !map.roofGrid.Roofed(c)) && c.Walkable(map);
    }
  }
}