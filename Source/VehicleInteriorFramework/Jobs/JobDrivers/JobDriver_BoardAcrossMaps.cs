using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors;

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
            VehiclePawn vehiclePawn = target as VehiclePawn;
            if (vehiclePawn == null)
            {
                if (!target.IsOnVehicleMapOf(out var vehiclePawnWithMap))
                {
                    Log.Error("[VehicleMapFramework] TargetA of JobDriver_BoardAcrossMaps must be VehiclePawn or on vehicle map.");
                    return;
                }
                vehiclePawn = vehiclePawnWithMap;
            }
            Lord lord = pawnBoarding.GetLord();
            if (lord?.LordJob is LordJob_FormAndSendVehicles lordJob_FormAndSendVehicles)
            {
                var vehicleAssigned = lordJob_FormAndSendVehicles.GetVehicleAssigned(pawnBoarding);
                vehicleAssigned.Vehicle.TryAddPawn(pawn, vehicleAssigned.handler);
            }
            else
            {
                vehiclePawn.BoardPawn(pawn);
                var caravan = vehiclePawn.GetCaravan() ?? vehiclePawn.GetVehicleCaravan();
                caravan?.AddPawn(pawnBoarding, true);
                Find.WorldPawns.PassToWorld(pawnBoarding, PawnDiscardDecideMode.Decide);
                if (pawnBoarding.Faction != vehiclePawn.Faction)
                {
                    pawnBoarding.SetFaction(vehiclePawn.Faction);
                }
                ThrowAppropriateHistoryEvent(vehiclePawn.VehicleDef.type, toil.actor);
            }
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
