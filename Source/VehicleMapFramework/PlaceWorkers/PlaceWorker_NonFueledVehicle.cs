using System.Linq;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_NonFueledVehicle : PlaceWorker
{
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
  {
    if (map.IsVehicleMapOf(out var vehicle) && vehicle.def.HasComp<CompFueledTravel>())
    {
      return true;
    }

    if (!ModsConfig.OdysseyActive) return "VMF_ForbidOnHumanPoweredVehicle".Translate();

    var occupied = GenAdj.OccupiedRect(loc, rot, checkingDef is ThingDef tDef ? tDef.Size : IntVec2.One);
    var engine = GravshipUtility.GetPlayerGravEngine_NewTemp(map);
    if (engine != null && occupied.All(engine.ValidSubstructureAt))
    {
      return true;
    }

    return "VMF_ForbidOnHumanPoweredVehicle".Translate();
  }
}
