using Verse;

namespace VehicleInteriors
{
    public class PlaceWorker_ForbidOnVehicle : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!map.IsVehicleMapOf(out _))
            {
                return true;
            }
            else
            {
                return "VMF_ForbidOnVehicle".Translate();
            }
        }
    }
}
