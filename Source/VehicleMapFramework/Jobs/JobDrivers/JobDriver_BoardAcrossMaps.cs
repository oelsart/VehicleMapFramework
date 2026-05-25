using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools.Rendering;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class JobDriver_BoardAcrossMaps : JobDriverAcrossMaps
{
  public override bool TryMakePreToilReservations(bool errorOnFailed)
  {
    return true;
  }

  protected override IEnumerable<Toil> MakeNewToils()
  {
    this.FailOnDespawnedOrNull(TargetIndex.A);
    this.FailOnForbidden(TargetIndex.A);
    //this.FailOnDowned(TargetIndex.A);
    foreach (var toil in GotoTargetMap(TargetIndex.A))
    {
      yield return toil;
    }
    yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
    yield return BoardVehicle(pawn);
  }

  private static Toil BoardVehicle(Pawn pawnBoarding)
  {
    Toil toil = new();
    toil.initAction = delegate
    {
      var target = pawnBoarding.jobs.curJob.GetTarget(TargetIndex.A).Thing;
      if (target is not VehiclePawn vehiclePawn)
      {
        if (!target.IsOnVehicleMapOf(out var vehiclePawnWithMap))
        {
          VMF_Log.Error("TargetA of JobDriver_BoardAcrossMaps must be VehiclePawn or on vehicle map.");
          return;
        }
        vehiclePawn = vehiclePawnWithMap;
      }
      var lord = pawnBoarding.GetLord();
      if (lord?.LordJob is LordJob_FormAndSendVehicles lordJob_FormAndSendVehicles)
      {
        var vehicleAssigned = Patch_JobDriver_Board_MakeNewToils.GetAssignedSeat(lordJob_FormAndSendVehicles, pawnBoarding);
        vehicleAssigned.Vehicle.TryAddPawn(pawnBoarding, vehicleAssigned.handler);
      }
      else
      {
        vehiclePawn.BoardPawn(pawnBoarding);
        ThrowAppropriateHistoryEvent(vehiclePawn.VehicleDef.type, toil.actor);
      }

      var targetHandler = vehiclePawn.handlers.OfType<VehicleRoleHandlerBuildable>()
        .FirstOrDefault(h => h.role is VehicleRoleBuildable buildable && buildable.upgradeComp.parent == target);
      targetHandler?.SetDirty();
    };
    toil.defaultCompleteMode = ToilCompleteMode.Instant;
    return toil;
  }

  private static void ThrowAppropriateHistoryEvent(VehicleType type, Pawn pawn)
  {
    if (ModsConfig.IdeologyActive)
    {
      switch (type)
      {
        case VehicleType.Sea:
          Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedSeaVehicle, pawn.Named(HistoryEventArgsNames.Doer)));
          return;
        case VehicleType.Air:
          Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedAirVehicle, pawn.Named(HistoryEventArgsNames.Doer)));
          return;
        case VehicleType.Land:
          Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedLandVehicle, pawn.Named(HistoryEventArgsNames.Doer)));
          return;
        case VehicleType.Universal:
          Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedUniversalVehicle, pawn.Named(HistoryEventArgsNames.Doer)));
          break;
        default:
          return;
      }
    }
  }
}
