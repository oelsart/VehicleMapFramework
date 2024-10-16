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
            var guest = pawn.Faction != Faction.OfPlayer;
            if (pawn.IsOnVehicleMapOf(out var vehicle) && (vehicle.AutoGetOff || guest))
            {
                var exitSpots = vehicle.interiorMap.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompVehicleEnterSpot>());
                var hostile = pawn.HostileTo(Faction.OfPlayer);
                var spot = exitSpots.FirstOrDefault(e => pawn.CanReach(e, PathEndMode.OnCell, Danger.Deadly, hostile, hostile, TraverseMode.ByPawn));
                if (spot != null)
                {
                    var job = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps);
                    var driver = job.GetCachedDriver(pawn) as JobDriverAcrossMaps;
                    driver.SetSpots(spot);
                    return job;
                }
            }
            return null;
        }
    }
}
