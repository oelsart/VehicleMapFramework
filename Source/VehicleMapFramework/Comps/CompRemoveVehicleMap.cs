using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class CompRemoveVehicleMap : ThingComp
{
  public override void Notify_PassedToWorld()
  {
    if (parent is VehiclePawnWithMap vehicle)
    {
      FrameDelay.DelayOne(v =>
        {
          if (v.IsWorldPawn() && v.ParentHolder is null) v.RemoveVehicleMap();
        },
        vehicle);
    }
  }
}
