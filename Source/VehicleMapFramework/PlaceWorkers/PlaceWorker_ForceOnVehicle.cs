using System.Linq;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_ForceOnVehicle : PlaceWorker
{
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
    Thing thingToIgnore = null, Thing thing = null)
  {
    if (map.IsVehicleMapOf(out _))
    {
      return true;
    }

    if (!ModsConfig.OdysseyActive) return "VMF_ForceOnVehicle".Translate();

    var occupied = GenAdj.OccupiedRect(loc, rot, checkingDef is ThingDef tDef ? tDef.Size : IntVec2.One);
    var engine = GravshipUtility.GetPlayerGravEngine_NewTemp(map);
    if (engine != null && occupied.All(engine.ValidSubstructureAt))
    {
      return true;
    }

    return "VMF_ForceOnVehicle".Translate();
  }
}