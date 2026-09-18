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
    var pawnFaction = pawn.Faction;
    var isPlayer = pawnFaction is { IsPlayer: true };
    if (isPlayer && !VehicleMapFramework.settings.autoGetOffPlayer ||
        !isPlayer && !VehicleMapFramework.settings.autoGetOffNonPlayer)
      return null;

    if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
    {
      if (!isPlayer && pawn.Faction == vehicle.Faction) return null;

      var cells = vehicle.VehicleRect().ExpandedBy(1).EdgeCells;

      var exitSpot = TargetInfo.Invalid;
      if (cells.Any(c => pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn,
            vehicle.Map, out exitSpot, out _, out _)))
      {
        var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps).SetSpotsToJobAcrossMaps(pawn, exitSpot);
        return job;
      }
    }

    return null;
  }
}