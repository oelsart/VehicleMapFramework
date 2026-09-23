using System;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using SmashTools;

namespace VehicleMapFramework;

public static class VehicleResizeUtility
{
  public static void ResizeNow(this VehiclePawnWithMap vehicle, bool reposition = true)
  {
    try
    {
      var vehicleDef = vehicle.VehicleDef;
      var curSize = vehicleDef.Size;
      var mapRect = vehicle.MapRect;
      var newRect = vehicle.ValidMapRect;
      var newSize = newRect.Size;
      if (curSize != newSize)
      {
        PreResize(vehicle);
        VMF_Log.DebugMessage($"Resize {vehicleDef} from {vehicleDef.size} to {newSize}");
        var prevSize = vehicleDef.size;
        vehicleDef.size = newSize;

        var offset = mapRect.CenterVector3 - newRect.CenterVector3;
        var drawOffsetComp = vehicle.CompVehicleDrawOffset;
        var prevOffset = drawOffsetComp?.drawOffset ?? Vector3.zero;
        if (drawOffsetComp is not null)
        {
          drawOffsetComp.drawOffset = offset;
          drawOffsetComp.drawOffsetNorth = offset;
          drawOffsetComp.drawOffsetEast = offset.RotatedBy(Rot4.East);
          drawOffsetComp.drawOffsetSouth = offset.RotatedBy(Rot4.South);
          drawOffsetComp.drawOffsetWest = offset.RotatedBy(Rot4.West);
        }

        if (vehicle.VehicleMapProps is VehicleMapProps_Unique { baseDef: { } baseDef })
        {
          vehicleDef.uiIconScale = Mathf.Max(baseDef.size.x, baseDef.size.z) / (Mathf.Max(newSize.x, newSize.z) + 1f);
          vehicleDef.properties.visibilityWeight = baseDef.properties.visibilityWeight *
            (vehicleDef.size.x + vehicleDef.size.z) / (baseDef.size.x + baseDef.size.z);
        }

        UniqueVehicleUtility.ReinitializeComponents(vehicleDef);
        UniqueVehicleUtility.GeneratePathData(vehicleDef);

        PostResize(vehicle);

        if (vehicle.Spawned)
        {
          var pos = vehicle.Position;
          if (reposition)
            Reposition(ref pos, vehicle, prevOffset - offset);

          Respawn(vehicle, pos, prevSize);
        }
        else if (vehicle.VehicleCaravanOrStashedVehicle?.GetComponent<VehicleFormationComp>() is { } formationComp &&
                 formationComp.DrawPositions.TryGetValue(vehicle, out var drawData))
        {
          var pos = drawData.cellRect.CenterCell;
          var delta = prevOffset - offset;
          pos += new IntVec3((int)MathF.Truncate(delta.x), 0, (int)MathF.Truncate(delta.z));
          if ((delta.x < 0f) == (vehicle.VehicleDef.Size.x % 2 == 1))
          {
            pos += IntVec3.East * (int)(delta.x % 1f * 2f);
          }

          if ((delta.z < 0f) == (vehicle.VehicleDef.Size.z % 2 == 1))
          {
            pos += IntVec3.North * (int)(delta.z % 1f * 2f);
          }

          drawData.cellRect = CellRect.CenteredOn(pos, newSize);
          formationComp.DrawPositions[vehicle] = drawData;

          foreach (var (vehicle2, drawData2) in formationComp.DrawPositions.ToArray())
          {
            if (vehicle == vehicle2) continue;
            if (drawData.cellRect.Overlaps(drawData2.cellRect))
            {
              formationComp.DrawPositions.Remove(vehicle);
              formationComp.FindVehiclePosition(vehicle);
              break;
            }
          }

          formationComp.CenteredDrawPositions();
        }

        FrameDelay.DelayOne(_vehicle => { _vehicle.VehicleMapGizmo.portrait.MarkDirty(); }, vehicle);
      }
    }
    catch (Exception ex)
    {
      VMF_Log.Error($"Error while resizing {vehicle.LabelCap}: {ex}");
    }
  }

  public static void PreResize(VehiclePawn vehicle)
  {
    if (vehicle.Spawned)
    {
      RegionListersUpdater.DeregisterInRegions(vehicle, vehicle.Map);
      vehicle.Map.thingGrid.Deregister(vehicle);
      vehicle.Map.coverGrid.DeRegister(vehicle);
    }

    if (vehicle is VehiclePawnWithMap vehiclePawnWithMap)
    {
      FrameDelay.DelayOne(_vehicle =>
      {
        _vehicle.impassableCellsDirty = true;
        _vehicle.mapEdgeCellsDirty = true;
        _vehicle.walkableCellsDirty = true;
        _vehicle.enterPositionsDirty = true;
      }, vehiclePawnWithMap);
    }
  }

  public static void PostResize(VehiclePawn vehicle)
  {
    if (vehicle is not VehiclePawnWithMap vehiclePawnWithMap) return;

    vehiclePawnWithMap.RecacheDrawPos(vehiclePawnWithMap.DrawPos);
    foreach (var handler in vehicle.Handlers)
    {
      if (handler.role is VehicleRoleBuildable vehicleRoleBuildable)
      {
        vehicleRoleBuildable.pawnRenderer?.SetDrawOffsets(vehiclePawnWithMap, vehicleRoleBuildable);
      }
    }
  }

  public static void Reposition(ref IntVec3 pos, VehiclePawn vehicle, Vector3 delta)
  {
    var rot = vehicle.Rotation;
    pos += new IntVec3((int)MathF.Truncate(delta.x), 0, (int)MathF.Truncate(delta.z)).RotatedBy(rot);
    var opp = Convert.ToInt32(rot.AsInt > 1);
    if ((delta.x < 0f) == (vehicle.VehicleDef.Size.x % 2 == opp))
    {
      pos += (IntVec3.East * (int)(delta.x % 1f * 2f)).RotatedBy(rot);
    }

    if ((delta.z < 0f) == (vehicle.VehicleDef.Size.z % 2 == opp))
    {
      pos += (IntVec3.North * (int)(delta.z % 1f * 2f)).RotatedBy(rot);
    }
  }

  public static void Respawn(VehiclePawnWithMap vehicle, IntVec3 pos, IntVec2 prevSize)
  {
    var rot = vehicle.Rotation;
    var map = vehicle.Map;

    var opp = rot.AsInt > 1;
    if (vehicle.VehicleDef.Size.x % 2 == 0 && opp)
    {
      pos.x += 1;
    }

    if (vehicle.VehicleDef.Size.z % 2 == 0 && opp)
    {
      pos.z += rot == Rot4.West ? -1 : 1;
    }

    if (!TryFindSpawnCell(vehicle, map, rot, ref pos))
    {
      VMF_Log.Error("No cells to respawn were found during resizing. The respawn process will be skipped.");
      return;
    }
    
    var selected = Find.Selector.IsSelected(vehicle);
    var newSize = vehicle.VehicleDef.size;
    vehicle.VehicleDef.size = prevSize;
    vehicle.DeSpawnWithoutJobClearVehicle(DestroyMode.WillReplace);
    vehicle.VehicleDef.size = newSize;
    GenSpawn.Spawn(vehicle, pos, map, rot);
    if (selected)
      Find.Selector.Select(vehicle, false, false);
  }

  private static bool TryFindSpawnCell(VehiclePawn vehicle, Map map, Rot4 rot, ref IntVec3 loc)
  {
    // Validate current position
    var positionManager = map.GetDetachedMapComponent<VehiclePositionManager>();
    var standable = true;
    foreach (var cell in vehicle.PawnOccupiedCells(loc, rot))
    {
      if (VehicleCanNotSpawnAt(vehicle, positionManager, map, cell))
      {
        standable = false;
        break;
      }
    }

    if (standable)
      return true; // If location is still valid, skip to spawning
    
    const int CloseRadialCheck = 30;
    const int FarRadialCheck = 100;
    if (!CellFinderExtended.TryRadialSearchForCell(loc, map, CloseRadialCheck, cell =>
        {
          foreach (var occupiedCell in vehicle.PawnOccupiedCells(cell, rot))
          {
            if (VehicleCanNotSpawnAt(vehicle, positionManager, map, occupiedCell))
              return false;
          }
          return true;
        }, out var newLoc))
    {
      // Just get the vehicle spawned in, user will need to dev-mode teleport them once loaded.
      // This is easier to handle than lost vehicles needing to be recovered from world pawns.
      Log.Error(
        $"Unable to find location to spawn {vehicle.LabelShort}. Performing wider search.");
      if (!CellFinderExtended.TryRadialSearchForCell(loc, map, FarRadialCheck, cell =>
          {
            foreach (var occupiedCell in vehicle.PawnOccupiedCells(cell, rot))
            {
              if (!occupiedCell.InBounds(map))
                return false;
            }

            return true;
          }, out newLoc))
      {
        Log.Error($"Unable to find location to spawn {vehicle.LabelShort}. Aborting spawn.");
        return false;
      }
    }

    loc = newLoc;
    return true;
  }
  
  private static bool VehicleCanNotSpawnAt(VehiclePawn vehicle, VehiclePositionManager positionManager, Map map, in IntVec3 cell)
  {
    return !cell.InBounds(map) || !cell.Walkable(vehicle.VehicleDef, map) ||
           positionManager.ClaimedBy(cell) is { } claimantVehicle && claimantVehicle != vehicle;
  }
}