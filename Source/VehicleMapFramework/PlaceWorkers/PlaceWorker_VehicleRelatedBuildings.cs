using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_VehicleRelatedBuildings : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null,
        Thing thing = null)
    {
        if (map.IsVehicleMapOf(out var vehicle) && checkingDef is ThingDef thingDef &&
            (vehicle.def.building?.relatedBuildCommands?.Contains(thingDef) ?? false))
        {
            return true;
        }
        return "VMF_ForceOnRelatedVehicle".Translate();
    }
}