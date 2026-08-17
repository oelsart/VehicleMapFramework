using System.Collections.Generic;
using RimWorld.Planet;
using Vehicles;
using Vehicles.World;

namespace VehicleMapFramework;

public static class VehicleCaravanHelper
{
  extension(WorldObject vehicleCaravanOrStashedVehicle)
  {
    public IEnumerable<VehiclePawn> Vehicles => vehicleCaravanOrStashedVehicle switch
    {
      VehicleCaravan caravan => caravan.Vehicles,
      StashedVehicle stashedVehicle => stashedVehicle.Vehicles,
      _ => []
    };
  }
}