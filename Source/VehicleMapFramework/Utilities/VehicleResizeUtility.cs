using System;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
#if DEV
using CoreLib.Performance;
#endif

namespace VehicleMapFramework;

public static class VehicleResizeUtility
{
    public static void ResizeNow(this VehiclePawnWithMap vehicle, bool changePosition = true)
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
        var offset = mapRect.CenterVector3 - newRect.CenterVector3;
        var data = vehicle.VehicleGraphic.DataRgb;
        var data2 = vehicle.VehicleDef.graphicData;
        var prevOffset = data.drawOffset;
        vehicleDef.size = newSize;
        data.drawOffset = data2.drawOffset =  offset;
        data.drawOffsetNorth = data2.drawOffsetNorth = offset;
        data.drawOffsetEast = data2.drawOffsetEast = offset.RotatedBy(Rot4.East);
        data.drawOffsetSouth = data2.drawOffsetSouth = offset.RotatedBy(Rot4.South);
        data.drawOffsetWest = data2.drawOffsetWest = offset.RotatedBy(Rot4.West);
        if (vehicleDef.GetModExtension<VehicleMapProps_Unique>() is { baseDef: { } baseDef })
        {
          vehicleDef.uiIconScale = (float)Mathf.Max(baseDef.size.x, baseDef.size.z) / Mathf.Max(newSize.x, newSize.z);
        }
        vehicle.VehicleMapGizmo.portrait.MarkDirty();
        UniqueVehicleUtility.ReinitializeComponents(vehicleDef);

        foreach (var map in Find.Maps)
        {
          if (map.IsVehicleMap) continue;

          var component = map.GetCachedMapComponent<VehiclePathingSystem>();
          UniqueVehicleUtility.GeneratePathData(component, SingleParam.Get(vehicleDef));
        }

        if (vehicle.Spawned)
        {
          if (changePosition)
            Reposition(vehicle, prevOffset - offset);
          else
          {
            vehicle.Map.thingGrid.Register(vehicle);
            vehicle.Map.coverGrid.Register(vehicle);
            RegionListersUpdater.RegisterInRegions(vehicle, vehicle.Map);
          }

          RefreshVehiclePather(vehicle);
        }
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
    
    public static void Reposition(VehiclePawn vehicle, Vector3 delta)
    {
        if (vehicle.Spawned)
        {
            vehicle.Position += new IntVec3(
                (int)MathF.Truncate(delta.x),
                0,
                (int)MathF.Truncate(delta.z)).RotatedBy(vehicle.Rotation);
            var opp = Convert.ToInt32(vehicle.Rotation.AsInt > 1);
            if ((delta.x < 0f) == (vehicle.VehicleDef.Size.x % 2 == opp))
            {
                vehicle.Position += (IntVec3.East * (int)(delta.x % 1f * 2f)).RotatedBy(vehicle.Rotation);
            }
            if ((delta.z < 0f) == (vehicle.VehicleDef.Size.z % 2 == opp))
            {
                vehicle.Position += (IntVec3.North * (int)(delta.z % 1f * 2f)).RotatedBy(vehicle.Rotation);
            }

            vehicle.DrawTracker.tweener.ResetTweenedPosToRoot();
            if (!vehicle.vehiclePather.Moving)
            {
                vehicle.vehiclePather.nextCell = vehicle.Position;
            }
        }
    }

    public static void RefreshVehiclePather(VehiclePawn vehicle)
    {
        var component = vehicle.Map.GetCachedMapComponent<VehiclePathingSystem>();
        UniqueVehicleUtility.SetPathData?.Invoke(vehicle.vehiclePather, SingleParam.Get(component[vehicle.VehicleDef]));
#if DEV
        if (!component.ThreadAvailable ||
            component.dedicatedThread.State == DedicatedThread.ThreadState.Running)
        {
            component.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
        }
        else
        {
            component.RequestGridsFor(vehicle);
        }
#else
        component.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
#endif
    }
}
