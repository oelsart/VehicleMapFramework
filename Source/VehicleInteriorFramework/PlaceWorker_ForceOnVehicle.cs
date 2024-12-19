using Verse;

namespace VehicleInteriors
{
    public class PlaceWorker_ForceOnVehicle : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            return map.IsVehicleMapOf(out _);
        }
    }
}
