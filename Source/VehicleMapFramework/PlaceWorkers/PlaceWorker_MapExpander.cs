using System.Linq;
using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_MapExpander : PlaceWorker
{
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
    Thing thingToIgnore = null, Thing thing = null)
  {
    if (!map.IsVehicleMapOf(out var vehicle))
    {
      return "VMF_ForbidOnVehicle".Translate();
    }

    if (!vehicle.ExpandableGrid[loc])
      return "VMF_ForceOnExpandableCell".Translate();

    foreach (var c in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
    {
      for (var i = 0; i < 4; i++)
      {
        var c2 = c + GenAdj.AdjacentCells[i];
        if (!c2.InBounds(map)) continue;

        foreach (var thing2 in c2.GetThingList(map))
        {
          if (thing2.def.PlaceWorkers is not { } placeWorkers) continue;
          foreach (var placeWorker in placeWorkers)
          {
            if (placeWorker is PlaceWorker_ForceOnVehicleMapEdge)
              return "VMF_ForceOnExpandableCell".Translate();
          }
        }
      }
    }

    return true;
  }
}