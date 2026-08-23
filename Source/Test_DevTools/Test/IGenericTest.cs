using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public interface IGenericTest
{
  VehicleGroup Group { get; set; }

  VehiclePawnWithMap Vehicle => (VehiclePawnWithMap)Group.vehicle;

  Pawn Pawn => Group.pawns[0];

  Map Map => Group.vehicle.Map;

  Map VehicleMap => Vehicle.VehicleMap;

  void SetGroup()
  {
    Group = DefaultVehicleGroup;
    TestUtils.ForceSpawn(Vehicle);
  }

  void DisposeGroup()
  {
    Group.Dispose();
  }
}
