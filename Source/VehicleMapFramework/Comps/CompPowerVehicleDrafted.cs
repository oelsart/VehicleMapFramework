using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompPowerVehicleDrafted : ThingComp
{
  protected CompPowerTrader PowerTrader => field ??= parent.GetComp<CompPowerTrader>();

  public override void PostSpawnSetup(bool respawningAfterLoad)
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      if (!parent.IsOnVehicleMapOf(out var vehicle))
        return;
      
      PowerTrader?.PowerOutput = vehicle.Drafted ? -PowerTrader.Props.PowerConsumption : 0f;
      vehicle.AddEvent(VehicleEventDefOf.IgnitionOn, Activate);
      vehicle.AddEvent(VehicleEventDefOf.IgnitionOff, Inactivate);
    });
  }

  public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
  {
    if (!map.IsVehicleMapOf(out var vehicle))
      return;
    
    vehicle.RemoveEvent(VehicleEventDefOf.IgnitionOn, Activate);
    vehicle.RemoveEvent(VehicleEventDefOf.IgnitionOff, Inactivate);
  }

  private void Activate() => PowerTrader?.PowerOutput = -PowerTrader.Props.PowerConsumption;

  private void Inactivate() => PowerTrader?.PowerOutput = 0f;
}