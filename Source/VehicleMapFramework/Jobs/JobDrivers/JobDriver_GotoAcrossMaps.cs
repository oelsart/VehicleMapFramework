using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class JobDriver_GotoAcrossMaps : JobDriverAcrossMaps
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return !job.targetA.IsValid || pawn.Reserve(DestMap, job.targetA.Cell, job);
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
            LocalTargetInfo lookAtTarget = job.GetTarget(TargetIndex.B);
            var toil = Toils_Goto.Goto(TargetIndex.A, PathEndMode.OnCell);
            toil.AddPreTickAction(delegate ()
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
            toil.FailOn(delegate ()
            {
                return job.GetTarget(TargetIndex.A).Thing is Pawn pawn && pawn.ParentHolder is Corpse;
            });
            toil.FailOn(delegate ()
            {
                Thing thing = job.GetTarget(TargetIndex.A).Thing;
                return thing != null && thing.Destroyed;
            });
            if (lookAtTarget.IsValid)
            {
                Toil toil2 = toil;
                toil2.tickAction = (Action)Delegate.Combine(toil2.tickAction, new Action(delegate ()
                {
                    pawn.rotationTracker.FaceCell(lookAtTarget.CellOnAnotherThingMap(pawn));
                }));
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
                    Pawn overseer = pawn.GetOverseer();
                    overseer?.mechanitor.GetControlGroup(pawn).SetTag(pawn, job.controlGroupTag);
                }
            });
            yield return toil;

            Toil toil3 = ToilMaker.MakeToil("MakeNewToils");
            toil3.initAction = delegate ()
            {
                if (pawn.mindState != null && pawn.mindState.forcedGotoPosition == TargetA.Cell)
                {
                    pawn.mindState.forcedGotoPosition = IntVec3.Invalid;
                }
                if (!job.ritualTag.NullOrEmpty())
                {
                    Lord lord = pawn.GetLord();
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
        pawn.ExitMap(true, CellRect.WholeMap(base.Map.BaseMap()).GetClosestEdge(pawn.Position));
    }
}
