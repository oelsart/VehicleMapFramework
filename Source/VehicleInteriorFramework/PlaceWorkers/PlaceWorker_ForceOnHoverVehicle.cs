using Verse;

namespace VehicleInteriors;

public class PlaceWorker_ForceOnHoverVehicle : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        if (map.IsVehicleMapOf(out var vehicle) && vehicle is VehiclePawnWithMap_Hover)
        {
            return true;
        }
        else
        {
            return "VMF_ForceOnHoverVehicle".Translate();
        }
    }
}
