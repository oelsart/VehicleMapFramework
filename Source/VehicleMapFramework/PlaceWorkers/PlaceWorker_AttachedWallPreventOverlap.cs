using RimWorld;
using System.Linq;
using Verse;

namespace VehicleMapFramework
{
    public class PlaceWorker_AttachedWallPreventOverlap : Placeworker_AttachedToWall
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            var report = base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!report.Accepted)
            {
                return report;
            }
            if (GenAdj.OccupiedRect(loc, rot, checkingDef.Size).Any(c => c.GetThingList(map).Any(t => t.def?.PlaceWorkers?.Any(p => p is PlaceWorker_AttachedWallPreventOverlap) ?? false)))
            {
                return "Occupied".Translate();
            }
            return true;
        }
    }
}
