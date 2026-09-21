using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

public static class VehicleMapCellFinder
{
  public static bool TryFindRandomEdgeCellWith([CanBeNull]Predicate<IntVec3> validator, VehiclePawnWithMap vehicle,
    out IntVec3 result)
  {
    if (validator is null)
    {
      result = RandomEdgeCell(vehicle);
      return true;
    }
      
    for (var i = 0; i < 100; i++)
    {
      result = RandomEdgeCell(vehicle);
      if (validator(result))
      {
        return true;
      }
    }

    var mapEdgeCells = SimplePool<List<IntVec3>>.Get();
    mapEdgeCells.AddRange(vehicle.CachedMapEdgeCells);
    mapEdgeCells.Shuffle();
    for (var i = 0; i < mapEdgeCells.Count; i++)
    {
      try
      {
        if (validator(mapEdgeCells[i]))
        {
          result = mapEdgeCells[i];
          mapEdgeCells.Clear();
          SimplePool<List<IntVec3>>.Return(mapEdgeCells);
          return true;
        }
      }
      catch (Exception ex)
      {
        VMF_Log.Error($"TryFindRandomEdgeCellWith exception validating {mapEdgeCells[i]}: {ex}");
      }
    }
    
    mapEdgeCells.Clear();
    SimplePool<List<IntVec3>>.Return(mapEdgeCells);
    result = IntVec3.Invalid;
    return false;
  }

  public static IntVec3 RandomEdgeCell(VehiclePawnWithMap vehicle)
  {
    return vehicle.CachedMapEdgeCells.RandomElement();
  }
}