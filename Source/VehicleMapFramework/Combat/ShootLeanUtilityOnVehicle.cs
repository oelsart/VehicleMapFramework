using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleMapFramework;

public static class ShootLeanUtilityOnVehicle
{
  private static readonly Queue<bool[]> blockedArrays = new();

  private static bool[] GetWorkingBlockedArray()
  {
    return blockedArrays.Count > 0 ? blockedArrays.Dequeue() : new bool[8];
  }

  private static void ReturnWorkingBlockedArray(bool[] ar)
  {
    blockedArrays.Enqueue(ar);
    if (blockedArrays.Count > 128)
    {
      Log.ErrorOnce("Too many blocked arrays to be feasible. >128", 388121);
    }
  }

  public static void CalcShootableCellsOf(List<IntVec3> outCells, Thing t, IntVec3 shooterPosOnBaseMap,
    AsAboveSoBelow.TargetBand? sourceBand, AsAboveSoBelow.TargetBand? targetBand)
  {
    outCells.Clear();
    switch (t)
    {
      case VehiclePawnWithMap vehicle:
      {
        //VehiclePawnWithMapへの射撃は壁がある場合その壁の場所を目標とする
        var cell = GenSight.LastPointOnLineOfSight(shooterPosOnBaseMap, t.Position, c =>
        {
          if (c.TryGetVehicleMap(t.Map, out var vehicle2) && vehicle == vehicle2)
          {
            var c2 = c.ToVehicleMapCoord(vehicle);
            var edifice = c2.GetEdificeSafe(vehicle.VehicleMap);
            if (edifice != null && !edifice.CanBeSeenOver())
            {
              return false;
            }
          }

          return true;
        });
        if (cell == IntVec3.Invalid)
        {
          cell = t.Position;
        }

        LeanShootingSourcesFromTo(cell, shooterPosOnBaseMap, t.Map, outCells, sourceBand, targetBand);
        return;
      }
      case Pawn:
        LeanShootingSourcesFromTo(t.Position, shooterPosOnBaseMap, t.Map, outCells, sourceBand, targetBand);
        return;
    }

    outCells.Add(t.Position);
    if (t.def.size.x != 1 || t.def.size.z != 1)
    {
      outCells.AddRange(t.OccupiedRect().Where(intVec => intVec != t.Position));
    }
  }

  public static void LeanShootingSourcesFromTo(IntVec3 shooterLoc, IntVec3 targetPosBaseCol, Map map,
    List<IntVec3> listToFill, AsAboveSoBelow.TargetBand? sourceBand, AsAboveSoBelow.TargetBand? targetBand)
  {
    var shooterLocBaseCol = shooterLoc;
    var baseMap = map.BaseMap();
    if (map.IsVehicleMapOf(out var vehicle))
    {
      shooterLocBaseCol = shooterLoc.ToBaseMapCoord(vehicle);
    }

    listToFill.Clear();
    var vector = (targetPosBaseCol - shooterLocBaseCol).ToVector3();
    if (vehicle is not null)
      vector = vector.RotatedBy(-vehicle.FullAngle);
    var angleFlat = vector.AngleFlat();
    var flag = angleFlat is > 270f or < 90f;
    var flag2 = angleFlat is > 90f and < 270f;
    var flag3 = angleFlat > 180f;
    var flag4 = angleFlat < 180f;
    var workingBlockedArray = GetWorkingBlockedArray();
    for (var i = 0; i < 8; i++)
    {
      var cell = shooterLoc + GenAdj.AdjacentCells[i];
      if (vehicle is not null)
        cell = cell.ToBaseMapCoord(vehicle);
      workingBlockedArray[i] = !cell.CanBeSeenOverOnVehicle(baseMap, sourceBand, targetBand);
    }

    if (!workingBlockedArray[1] && ((workingBlockedArray[0] && !workingBlockedArray[5] && flag) ||
                                    (workingBlockedArray[2] && !workingBlockedArray[4] && flag2)))
    {
      listToFill.Add(shooterLoc + new IntVec3(1, 0, 0));
    }

    if (!workingBlockedArray[3] && ((workingBlockedArray[0] && !workingBlockedArray[6] && flag) ||
                                    (workingBlockedArray[2] && !workingBlockedArray[7] && flag2)))
    {
      listToFill.Add(shooterLoc + new IntVec3(-1, 0, 0));
    }

    if (!workingBlockedArray[2] && ((workingBlockedArray[3] && !workingBlockedArray[7] && flag3) ||
                                    (workingBlockedArray[1] && !workingBlockedArray[4] && flag4)))
    {
      listToFill.Add(shooterLoc + new IntVec3(0, 0, -1));
    }

    if (!workingBlockedArray[0] && ((workingBlockedArray[3] && !workingBlockedArray[6] && flag3) ||
                                    (workingBlockedArray[1] && !workingBlockedArray[5] && flag4)))
    {
      listToFill.Add(shooterLoc + new IntVec3(0, 0, 1));
    }

    if (shooterLocBaseCol.CanBeSeenOverOnVehicle(baseMap, sourceBand, targetBand))
    {
      listToFill.Add(shooterLoc);
    }

    for (var j = 0; j < 4; j++)
    {
      var cell = shooterLoc + GenAdj.AdjacentCells[j];
      if (!workingBlockedArray[j] && (j != 0 || flag) && (j != 1 || flag4) && (j != 2 || flag2) && (j != 3 || flag3) &&
          cell.InBounds(map) && cell.GetCover(map) is not null)
      {
        listToFill.Add(cell);
      }
    }

    ReturnWorkingBlockedArray(workingBlockedArray);
  }
}