using System.Linq;
using RimWorld;
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
        if (GenAdj.OccupiedRect(loc, rot, checkingDef.Size).Any(c =>
            {
                return !vehicle.CachedExpandableCells.Contains(loc) ||
                       loc.GetEdifice(map)?.def != VMF_DefOf.VMF_VehicleStructureEmpty ||
                       (c + c.DirectionToInsideMap(vehicle).AsIntVec3).GetThingList(vehicle.VehicleMap)
                       .Any(t => t.def.PlaceWorkers?.Any(p => p is PlaceWorker_ForceOnVehicleMapEdge) ?? false);
            }))
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
