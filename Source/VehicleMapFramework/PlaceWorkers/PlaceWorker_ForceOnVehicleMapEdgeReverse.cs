using System.Linq;
using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_ForceOnVehicleMapEdgeReverse : PlaceWorker_ForceOnVehicleMapEdge
{
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
    Thing thingToIgnore = null, Thing thing = null)
  {
    if (!map.IsVehicleMapOf(out var vehicle))
    {
      return "VMF_ForceOnVehicle".Translate();
    }

    if (GenAdj.OccupiedRect(loc, rot, checkingDef.Size)
        .Select(cell => cell + rot.FacingCell)
        .Any(facingCell =>
          !vehicle.OutOfBoundsGrid[facingCell] &&
          (!vehicle.ExpandableGrid[facingCell] ||
           !vehicle.ImpassableCellGrid[facingCell])))
    {
      return "VMF_ForceOnVehicleMapEdge".Translate();
    }

    return true;
  }
}