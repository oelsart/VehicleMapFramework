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
            if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                var hostile = pawn.HostileTo(Faction.OfPlayer);
                var cells = vehicle.VehicleRect().ExpandedBy(1).EdgeCells;

                TargetInfo exitSpot = TargetInfo.Invalid;
                if (cells.Any(c => pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, hostile, hostile, TraverseMode.ByPawn, vehicle.Map, out exitSpot, out _)))
                {
                    var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot);
                    return job;
                }
            }
            return null;
        }
    }
}
