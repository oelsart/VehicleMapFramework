using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobGiver_GetOffVehicle : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn) => 0f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.IsOnVehicleMapOf(out var vehicle))
            {
                var exitSpots = vehicle.interiorMap.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompVehicleEnterSpot>());
                var hostile = pawn.HostileTo(Faction.OfPlayer);
                var spot = exitSpots.FirstOrDefault(e => pawn.CanReach(e, PathEndMode.OnCell, Danger.Deadly, hostile, hostile, TraverseMode.ByPawn));
                if (spot != null)
                {
                    return JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps, spot);
                }
            }
            return null;
        }
    }
}
