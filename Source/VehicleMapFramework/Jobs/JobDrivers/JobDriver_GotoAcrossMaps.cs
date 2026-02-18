using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Debug = UnityEngine.Debug;

namespace VehicleMapFramework;

public class JobDriver_GotoAcrossMaps : JobDriverAcrossMaps
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        DestMap.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        if (ShouldEnterTargetAMap)
        {
            foreach (var toil in GotoTargetMap(TargetIndex.A)) yield return toil;
        }
        if (ShouldEnterTargetBMap)
        {
            foreach (var toil in GotoTargetMap(TargetIndex.B)) yield return toil;
        }
        if (job.targetA.IsValid)
        {
            var lookAtTarget = job.GetTarget(TargetIndex.B);
            var toil = Toils_Goto.Goto(TargetIndex.A, PathEndMode.OnCell);
            toil.AddPreTickAction(delegate
            {
                if (job.exitMapOnArrival && pawn.Map.exitMapGrid.IsExitCell(pawn.Position))
                {
                    TryExitMap();
                }
                if (pawn is VehiclePawn vehicle && job.exitMapOnArrival && vehicle.InhabitedCells(1).NotNullAndAny(cell => pawn.BaseMap().exitMapGrid.IsExitCell(cell)))
                {
                    PathingHelper.ExitMapForVehicle(vehicle, job);
                }
            });
            toil.FailOn(() => job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(pawn));
            toil.FailOn(() => job.GetTarget(TargetIndex.A).Thing is Pawn { ParentHolder: Corpse });
            toil.FailOn(() =>
            {
                var thing = job.GetTarget(TargetIndex.A).Thing;
                return thing is { Destroyed: true };
            });
            if (lookAtTarget.IsValid)
            {
                toil.tickAction += delegate
                {
                    pawn.rotationTracker.FaceCell(lookAtTarget.CellOnAnotherThingMap(pawn));
                };
                toil.handlingFacing = true;
            }
            toil.AddFinishAction(delegate
            {
                if (job.controlGroupTag == null)
                {
                    return;
                }
                if (job.controlGroupTag != null)
                {
                    var overseer = pawn.GetOverseer();
                    overseer?.mechanitor.GetControlGroup(pawn).SetTag(pawn, job.controlGroupTag);
                }
            });
            yield return toil;

            var toil3 = ToilMaker.MakeToil();
            toil3.initAction = delegate
            {
                if (pawn.mindState != null && pawn.mindState.forcedGotoPosition == TargetA.Cell)
                {
                    pawn.mindState.forcedGotoPosition = IntVec3.Invalid;
                }
                if (!job.ritualTag.NullOrEmpty())
                {
                    var lord = pawn.GetLord();
                    if (lord?.LordJob is LordJob_Ritual lordJob_Ritual)
                    {
                        lordJob_Ritual.AddTagForPawn(pawn, job.ritualTag);
                    }
                }
                if (job.exitMapOnArrival && !pawn.IsOnVehicleMapOf(out _) && (pawn.Position.OnEdge(pawn.Map) || pawn.Map.exitMapGrid.IsExitCell(pawn.Position)))
                {
                    TryExitMap();
                }
            };
            toil3.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil3;
        }
    }

    private void TryExitMap()
    {
        if (job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(pawn))
        {
            return;
        }
        if (ModsConfig.BiotechActive)
        {
            MechanitorUtility.Notify_PawnGotoLeftMap(pawn, pawn.BaseMap());
        }
        if (ModsConfig.AnomalyActive && !MetalhorrorUtility.TryPawnExitMap(pawn))
        {
            return;
        }
        pawn.ExitMap(true, CellRect.WholeMap(Map.BaseMap()).GetClosestEdge(pawn.Position));
    }
}
