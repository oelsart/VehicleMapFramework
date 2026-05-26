using Vehicles;

namespace VehicleMapFramework;

public class CompProperties_ElectricMapVehicle : VehicleCompProperties
{
  public CompProperties_ElectricMapVehicle()
  {
    compClass = typeof(CompElectricMapVehicle);
  }
}
