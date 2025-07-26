using RimWorld;
using System.Linq;
using Verse;

namespace VehicleMapFramework
{
    public class PlaceWorker_AttachedWallMultiCell : Placeworker_AttachedToWall
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (GenAdj.OccupiedRect(loc, rot, checkingDef.Size).Any(c => c.GetThingList(map).Any(t => t.def?.PlaceWorkers?.Any(p => p is PlaceWorker_AttachedWallMultiCell) ?? false)))
            {
                return "Occupied".Translate();
            }

            var occupiedRect = GenAdj.OccupiedRect(loc, rot, checkingDef.Size);

            AcceptanceReport report = AcceptanceReport.WasRejected;
            report = base.AllowsPlacing(checkingDef, occupiedRect.GetCenterCellOnEdge(rot), rot, map, thingToIgnore, thing);
            if (report.Accepted) return report;
            if (occupiedRect.GetSideLength(rot) % 2 == 0)
            {
                report = base.AllowsPlacing(checkingDef, occupiedRect.GetCenterCellOnEdge(rot, -1), rot, map, thingToIgnore, thing);
            }
            return report;
        }
    }
}
