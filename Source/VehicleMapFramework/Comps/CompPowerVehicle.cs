using RimWorld;

namespace VehicleMapFramework;

public class CompPowerVehicle : CompPowerTrader
{
  public override void CompTickInterval(int delta)
  {
    base.CompTickInterval(delta);
    PowerOutput = parent.IsOnVehicleMapOf(out var vehicle) && vehicle.Drafted ? -Props.PowerConsumption : 0f;
  }
}