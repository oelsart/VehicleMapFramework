using System.Collections.Generic;
using RimWorld.Planet;
using Vehicles;
using Vehicles.World;

namespace VehicleMapFramework;

public static class VehicleCaravanHelper
{
  // TODO VF Updates: StashedVehicleにもIVehicleWorldObjectを実装するPRを提出済み
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