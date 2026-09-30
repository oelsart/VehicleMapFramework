using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapGUI(Map map) : MapComponent(map)
{
  public override void MapComponentOnGUI()
  {
    if (Event.current.type is not EventType.Repaint || !map.IsVehicleMapOf(out var vehicle))
      return;
    
    vehicle.DrawGUIOverlay();

    if (vehicle.VehicleCaravanOrStashedVehicle is { } vehicleCaravanOrStashedVehicle)
    {
      foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
      {
        vehicle2.DrawGUIOverlay();
      }
    }
  }
}