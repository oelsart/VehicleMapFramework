using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobGiver_GetOffVehicle : ThinkNode_JobGiver
{
    public override float GetPriority(Pawn pawn) => 0f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (pawn.Faction?.IsPlayer ?? false)
        {
            if (!VehicleMapFramework.settings.autoGetOffPlayer) return null;
        }
        else if (!VehicleMapFramework.settings.autoGetOffNonPlayer) return null;
        if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            var cells = vehicle.VehicleRect().ExpandedBy(1).EdgeCells;

            TargetInfo exitSpot = TargetInfo.Invalid;
            if (cells.Any(c => pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, vehicle.Map, out exitSpot, out _)))
            {
                var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot);
                return job;
            }
        }
        return null;
    }
}
