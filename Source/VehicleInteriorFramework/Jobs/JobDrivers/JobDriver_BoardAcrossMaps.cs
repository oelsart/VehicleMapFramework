using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors
{
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
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
            yield return this.BoardVehicle(this.pawn);
        }

        private Toil BoardVehicle(Pawn pawnBoarding)
        {
            Toil toil = new Toil();
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
                ValueTuple<VehiclePawn, VehicleRoleHandler> vehicleAssigned;
                if (lord?.LordJob is LordJob_FormAndSendVehicles lordJob_FormAndSendVehicles)
                {
                    vehicleAssigned = lordJob_FormAndSendVehicles.GetVehicleAssigned(pawnBoarding);
                }
                else
                {
                    Bill_BoardVehicle bill_BoardVehicle = vehiclePawn.bills.FirstOrDefault((Bill_BoardVehicle b) => b.pawnToBoard == pawnBoarding);
                    VehicleRoleHandler vehicleHandler = bill_BoardVehicle?.handler;
                    if (vehicleHandler == null && vehiclePawn.Spawned)
                    {
                        VehicleHandlerReservation reservation = MapComponentCache<VehicleReservationManager>.GetComponent(vehiclePawn.Map).GetReservation<VehicleHandlerReservation>(vehiclePawn);
                        vehicleHandler = reservation.ReservedHandler(pawnBoarding);
                        if (vehicleHandler == null)
                        {
                            Log.Error("Could not find assigned spot for " + pawnBoarding.LabelShort + " to board.");
                            return;
                        }
                    }
                    vehicleAssigned = new ValueTuple<VehiclePawn, VehicleRoleHandler>(vehiclePawn, vehicleHandler);
                }
                if (vehicleAssigned.Item2 == null)
                {
                    Log.Error("[VehicleMapFramework] VehicleRoleHandler is null. This should never happen as assigned seating either handles arrangements or instructs pawns to follow rather than board.");
                }
                vehicleAssigned.Item1.GiveLoadJob(pawnBoarding, vehicleAssigned.Item2);
                if (vehiclePawn.Spawned)
                {
                    vehicleAssigned.Item1.Notify_Boarded(pawnBoarding);
                }
                else
                {
                    if (pawnBoarding.Spawned)
                    {
                        pawnBoarding.DeSpawn();
                    }
                    var caravan = vehiclePawn.GetCaravan() ?? vehiclePawn.GetVehicleCaravan();
                    caravan?.AddPawn(pawnBoarding, true);
                    Find.WorldPawns.PassToWorld(pawnBoarding, PawnDiscardDecideMode.Decide);
                    if (pawnBoarding.Faction != vehiclePawn.Faction)
                    {
                        pawnBoarding.SetFaction(vehiclePawn.Faction);
                    }
                    vehicleAssigned.Item1.Notify_BoardedCaravan(pawnBoarding, vehicleAssigned.Item2.thingOwner);
                    var bill = vehicleAssigned.Item1.bills.FirstOrDefault(b => b.pawnToBoard == pawnBoarding);
                    if (bill != null)
                    {
                        vehicleAssigned.Item1.bills.Remove(bill);
                    }
                }
                this.ThrowAppropriateHistoryEvent(vehiclePawn.VehicleDef.type, toil.actor);
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
}
