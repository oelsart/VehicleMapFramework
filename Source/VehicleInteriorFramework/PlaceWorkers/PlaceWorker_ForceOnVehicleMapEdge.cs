using Verse;

namespace VehicleInteriors
{
    public class PlaceWorker_ForceOnVehicleMapEdge : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (map.IsVehicleMapOf(out _) && !(loc - rot.FacingCell).InBounds(map))
            {
                return true;
            }
            else
            {
                return "VIF.ForceOnVehicleMapEdge".Translate();
            }
        }
    }
}
