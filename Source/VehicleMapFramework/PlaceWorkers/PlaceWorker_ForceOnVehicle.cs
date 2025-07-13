using RimWorld;
using System.Linq;
using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_ForceOnVehicle : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        if (map.IsVehicleMapOf(out _))
        {
            return true;
        }
        if (ModsConfig.OdysseyActive)
        {
            var occupied = GenAdj.OccupiedRect(loc, rot, checkingDef is ThingDef tDef ? tDef.Size : IntVec2.One);
            if (GravshipUtility.GetPlayerGravEngine(map) is Building_GravEngine engine && occupied.All(engine.ValidSubstructureAt))
            {
                return true;
            }
        }
        return "VMF_ForceOnVehicle".Translate();
    }
}
