using System;
using System.Linq;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
#if DEV
#endif

namespace VehicleMapFramework;

public static class VehicleResizeUtility
{
  public static void ResizeNow(this VehiclePawnWithMap vehicle, bool reposition = true)
  {
    var vehicleDef = vehicle.VehicleDef;
    var curSize = vehicleDef.Size;
    var mapRect = CellRect.WholeMap(vehicle.VehicleMap);
    var newRect = vehicle.ValidMapRect;
    var newSize = newRect.Size;
    if (curSize != newSize)
    {
      PreResize(vehicle);
      VMF_Log.DebugMessage($"Resize {vehicleDef} from {vehicleDef.size} to {newSize}");
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
      }
      
      UniqueVehicleUtility.ReinitializeComponents(vehicleDef);
      
#if DEV
      var calculator =
        Activator.CreateInstance(GenTypes.GetTypeInAnyAssembly("Vehicles.PathGridCalculator", "Vehicles"));
      foreach (var map in Find.Maps)
      {
        if (map.IsVehicleMap) continue;

        var component = map.GetCachedMapComponent<VehiclePathingSystem>();
        UniqueVehicleUtility.GeneratePathData(component.PathData,
          Params<(object, object, object)>.Get((calculator, vehicleDef, component.PathFinder)));
      }
#endif

      PostResize(vehicle);

      if (vehicle.Spawned)
      {
        var pos = vehicle.Position;
        if (reposition)
          Reposition(ref pos, vehicle, prevOffset - offset);
        
        Respawn(vehicle, pos);
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

      if (UnityData.IsInMainThread)
        vehicle.VehicleMapGizmo.portrait.MarkDirty();
      else
        LongEventHandler.ExecuteWhenFinished(() => vehicle.VehicleMapGizmo.portrait.MarkDirty());
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

  public static void Respawn(VehiclePawnWithMap vehicle, IntVec3 pos)
  {
    var rot = vehicle.Rotation;
    var map = vehicle.Map;
    var selected = Find.Selector.IsSelected(vehicle);
    vehicle.DeSpawnWithoutJobClearVehicle(DestroyMode.WillReplace);

    var opp = rot.AsInt > 1;
    if (vehicle.VehicleDef.Size.x % 2 == 0 && opp)
    {
      pos.x += 1;
    }

    if (vehicle.VehicleDef.Size.z % 2 == 0 && opp)
    {
      pos.z += rot == Rot4.West ? -1 : 1;
    }

    GenSpawn.Spawn(vehicle, pos, map, rot);
    if (selected)
      Find.Selector.Select(vehicle, false, false);
  }
}