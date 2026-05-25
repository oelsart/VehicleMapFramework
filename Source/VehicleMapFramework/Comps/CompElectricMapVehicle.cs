using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompElectricMapVehicle : VehicleComp
{
  public CompFueledTravel CompFueledTravel => field ??= Vehicle.GetComp<CompFueledTravel>();

  public override bool TickByRequest => true;

  public override void CompTickInterval(int delta)
  {
    base.CompTickInterval(delta);
    if (VehicleFramework.connectedPower(CompFueledTravel) is not null ||
        Vehicle is not VehiclePawnWithMap vehiclePawnWithMap) return;

    if (!ConnectPower(vehiclePawnWithMap.VehicleMap, CompFueledTravel) && MultiFloors.Active)
    {
      foreach (var map in MultiFloors.GetOtherLevels(vehiclePawnWithMap.VehicleMap))
      {
        if (ConnectPower(map, CompFueledTravel)) break;
      }
    }
    return;

    static bool ConnectPower(Map map, CompFueledTravel comp)
    {
      foreach (var powerNet in map.powerNetManager.AllNetsListForReading)
      {
        var chargeRate = comp.Props.chargeRate;
        if (powerNet.CurrentStoredEnergy() <= chargeRate && powerNet.CurrentEnergyGainRate() <= chargeRate)
          continue;

        var transmitter = powerNet.transmitters.FirstOrDefault(t => t.TransmitsPowerNow);
        if (transmitter is not null)
        {
          VehicleFramework.connectedPower(comp) = transmitter;
          return true;
        }
      }
      return false;
    }
  }
}
