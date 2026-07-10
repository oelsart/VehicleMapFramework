using System.Linq;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompNpcVehicleMap : VehicleComp
{
  public CompProperties_NpcVehicleMap Props => (CompProperties_NpcVehicleMap)props;

  public CompProperties_NpcVehicleMap.VehicleMapParams Params { get; private set; }

  public void SetParams(int pawnCount)
  {
    if (Params is not null)
    {
      VMF_Log.Warning("CompNpcVehicleMap: Params already set.");
      return;
    }

    if (Props.mapParams
        .Where(mapParams => mapParams.pawnCountRange.InRange(pawnCount))
        .TryRandomElement(out var result))
    {
      Params = result;
      return;
    }

    VMF_Log.Warning($"CompNpcVehicleMap: No mapParams found for pawnCount {pawnCount}. Using first mapParams.");
    Params = Props.mapParams.FirstOrDefault();
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    var vehicleMapParams = Params;
    Scribe_Deep.Look(ref vehicleMapParams, "params");
    Params = vehicleMapParams;
  }
}
