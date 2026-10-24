namespace VehicleMapFramework.Test;

public readonly struct DynamicPatchEnabler : IDisposable
{
  private readonly bool dynamicPatchEnabled = VehicleMapFramework.Settings.dynamicPatchEnabled;
  private readonly bool dynamicUnPatchEnabled = VehicleMapFramework.Settings.dynamicUnpatchEnabled;

  public DynamicPatchEnabler()
  {
    VehicleMapFramework.Settings.dynamicPatchEnabled = true;
    VehicleMapFramework.Settings.dynamicUnpatchEnabled = true;
  }

  void IDisposable.Dispose()
  {
    VehicleMapFramework.Settings.dynamicPatchEnabled = dynamicPatchEnabled;
    VehicleMapFramework.Settings.dynamicUnpatchEnabled = dynamicUnPatchEnabled;
  }
}
