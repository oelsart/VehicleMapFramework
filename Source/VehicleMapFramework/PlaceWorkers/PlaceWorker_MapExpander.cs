using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_MapExpander : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        if (!map.IsVehicleMapOf(out var vehicle))
        {
            return "VMF_ForbidOnVehicle".Translate();
        }
        if (!vehicle.CachedExpandableCells.Contains(loc) || loc.GetEdifice(map)?.def != VMF_DefOf.VMF_VehicleStructureEmpty)
        {
            return "VMF_ForceOnExpandableCell".Translate();
        }
        return true;
    }

    public override bool ForceAllowPlaceOver(BuildableDef other)
    {
        return other == VMF_DefOf.VMF_VehicleStructureEmpty;
    }
}
