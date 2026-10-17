namespace VehicleMapFramework;

public struct FocusVehicleScope : IDisposable
{
  public static VehiclePawnWithMap FocusLockedVehicle { get; set; }

  public static VehiclePawnWithMap FocusedVehicle { get; set; }
  
  private readonly VehiclePawnWithMap tmpFocused;

  public FocusVehicleScope(VehiclePawnWithMap vehicle)
  {
    tmpFocused = FocusedVehicle;
    FocusedVehicle = vehicle;
  }

  public static FocusVehicleScope FocusVehicle(VehiclePawnWithMap vehicle)
  {
    return new FocusVehicleScope(vehicle);
  }

  public void Dispose()
  {
    FocusedVehicle = tmpFocused;
  }
}