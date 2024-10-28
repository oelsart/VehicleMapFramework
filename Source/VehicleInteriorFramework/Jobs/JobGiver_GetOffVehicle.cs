using RimWorld;
using System.Linq;
using Vehicles;
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
            if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned && (vehicle.AutoGetOff || guest))
            {
                var exitSpots = vehicle.interiorMap.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompVehicleEnterSpot>());
                var hostile = pawn.HostileTo(Faction.OfPlayer);
                var spot = exitSpots.FirstOrDefault(e =>
                {
                    var baseMap = e.BaseMapOfThing();
                    var basePos = e.PositionOnBaseMap();
                    var faceCell = e.BaseFullRotationOfThing().FacingCell;
                    faceCell.y = 0;
                    var dist = 1;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    var cell = (basePos - faceCell * dist);
                    return pawn.CanReach(e, PathEndMode.OnCell, Danger.Deadly, hostile, hostile, TraverseMode.ByPawn) && cell.Walkable(baseMap);
                });
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
