using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_ForceOnVehicleMapEdge : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        if (!map.IsVehicleMapOf(out var vehicle))
        {
            return "VMF_ForceOnVehicle".Translate();
        }
        var facingCell = loc - rot.FacingCell;
        if (vehicle.CachedOutOfBoundsCells.Contains(facingCell) || vehicle.CachedExpandableCells.Contains(facingCell) && vehicle.CachedStructureCells.Contains(facingCell))
        {
            return true;
        }
        return "VMF_ForceOnVehicleMapEdge".Translate();
    }
}
