using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public interface IGenericTest
{
    VehicleGroup Group { get; set; }

    public VehiclePawnWithMap Vehicle => (VehiclePawnWithMap)Group.vehicle;

    public Pawn Pawn => Group.pawns[0];

    public Map Map => Group.vehicle.Map;

    Map VehicleMap => Vehicle.VehicleMap;

    public void SetGroup()
    {
        Group = DefaultVehicleGroup;
        TestUtils.ForceSpawn(Vehicle);
        Vehicle.DoTick();
    }

    public void DisposeGroup()
    {
        Group.Dispose();
    }
}
