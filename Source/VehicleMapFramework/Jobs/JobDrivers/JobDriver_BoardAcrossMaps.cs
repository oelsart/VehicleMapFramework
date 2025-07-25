using RimWorld;
using SmashTools.Rendering;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleMapFramework;

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
        if (ShouldEnterTargetAMap)
        {
            foreach (var toil in GotoTargetMap(TargetIndex.A)) yield return toil;
        }
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
        yield return BoardVehicle(pawn);
    }

    private Toil BoardVehicle(Pawn pawnBoarding)
    {
        Toil toil = new();
        toil.initAction = delegate ()
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
            Lord lord = pawnBoarding.GetLord();
            if (lord?.LordJob is LordJob_FormAndSendVehicles lordJob_FormAndSendVehicles)
            {
                var vehicleAssigned = lordJob_FormAndSendVehicles.GetVehicleAssigned(pawnBoarding);
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

    private void ThrowAppropriateHistoryEvent(VehicleType type, Pawn pawn)
    {
        if (ModsConfig.IdeologyActive)
        {
            switch (type)
            {
                case VehicleType.Sea:
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedSeaVehicle, pawn.Named(HistoryEventArgsNames.Doer)), true);
                    return;
                case VehicleType.Air:
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedAirVehicle, pawn.Named(HistoryEventArgsNames.Doer)), true);
                    return;
                case VehicleType.Land:
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedLandVehicle, pawn.Named(HistoryEventArgsNames.Doer)), true);
                    return;
                case VehicleType.Universal:
                    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf_Vehicles.VF_BoardedUniversalVehicle, pawn.Named(HistoryEventArgsNames.Doer)), true);
                    break;
                default:
                    return;
            }
        }
    }
}
