using Verse;

namespace VehicleInteriors
{
    public class PlaceWorker_ForceOnVehicleMapEdge : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            IntVec3 facingCell;
            if (map.IsVehicleMapOf(out var vehicle) && (!(facingCell = loc - rot.FacingCell).InBounds(map) || vehicle.CachedOutOfBoundsCells.Contains(facingCell)))
            {
                return true;
            }
            else
            {
                return "VMF_ForceOnVehicleMapEdge".Translate();
            }
        }
    }
}
