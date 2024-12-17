using RimWorld.Planet;
using RimWorld;
using SmashTools;
using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;
using System;
using Verse.AI.Group;

namespace VehicleInteriors
{
    public class JobDriver_GotoAcrossMaps : JobDriverAcrossMaps
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return !this.job.targetA.IsValid || this.pawn.Reserve(this.DestMap, this.job.targetA.Cell, this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil in this.GotoTargetMap(TargetIndex.B)) yield return toil;
            }
            if (job.targetA.IsValid)
            {
                LocalTargetInfo lookAtTarget = this.job.GetTarget(TargetIndex.B);
                var toil = Toils_Goto.Goto(TargetIndex.A, PathEndMode.OnCell);
                toil.AddPreTickAction(delegate ()
                {
                    if (this.job.exitMapOnArrival && this.pawn.Map.exitMapGrid.IsExitCell(this.pawn.Position))
                    {
                        this.TryExitMap();
                    }
                    if (this.pawn is VehiclePawn vehicle && this.job.exitMapOnArrival && vehicle.InhabitedCells(1).NotNullAndAny(cell => this.pawn.BaseMap().exitMapGrid.IsExitCell(cell)))
                    {
                        PathingHelper.ExitMapForVehicle(vehicle, this.job);
                    }
                });
                toil.FailOn(() => this.job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(this.pawn));
                toil.FailOn(delegate ()
                {
                    Pawn pawn;
                    return (pawn = (this.job.GetTarget(TargetIndex.A).Thing as Pawn)) != null && pawn.ParentHolder is Corpse;
                });
                toil.FailOn(delegate ()
                {
                    Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
                    return thing != null && thing.Destroyed;
                });
                if (lookAtTarget.IsValid)
                {
                    Toil toil2 = toil;
                    toil2.tickAction = (Action)Delegate.Combine(toil2.tickAction, new Action(delegate ()
                    {
                        this.pawn.rotationTracker.FaceCell(lookAtTarget.CellOnAnotherThingMap(this.pawn));
                    }));
                    toil.handlingFacing = true;
                }
                toil.AddFinishAction(delegate
                {
                    if (this.job.controlGroupTag == null)
                    {
                        return;
                    }
                    if (this.job.controlGroupTag != null)
                    {
                        Pawn overseer = this.pawn.GetOverseer();
                        if (overseer != null)
                        {
                            overseer.mechanitor.GetControlGroup(this.pawn).SetTag(this.pawn, this.job.controlGroupTag);
                        }
                    }
                });
                yield return toil;

                Toil toil3 = ToilMaker.MakeToil("MakeNewToils");
                toil3.initAction = delegate ()
                {
                    if (this.pawn.mindState != null && this.pawn.mindState.forcedGotoPosition == this.TargetA.Cell)
                    {
                        this.pawn.mindState.forcedGotoPosition = IntVec3.Invalid;
                    }
                    if (!this.job.ritualTag.NullOrEmpty())
                    {
                        Lord lord = this.pawn.GetLord();
                        if (lord?.LordJob is LordJob_Ritual lordJob_Ritual)
                        {
                            lordJob_Ritual.AddTagForPawn(this.pawn, this.job.ritualTag);
                        }
                    }
                    if (this.job.exitMapOnArrival && !this.pawn.IsOnVehicleMapOf(out _) && (this.pawn.Position.OnEdge(this.pawn.Map) || this.pawn.Map.exitMapGrid.IsExitCell(this.pawn.Position)))
                    {
                        this.TryExitMap();
                    }
                };
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return toil3;
            }
        }

        private void TryExitMap()
        {
            if (this.job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(this.pawn))
            {
                return;
            }
            if (ModsConfig.BiotechActive)
            {
                MechanitorUtility.Notify_PawnGotoLeftMap(this.pawn, this.pawn.BaseMap());
            }
            if (ModsConfig.AnomalyActive && !MetalhorrorUtility.TryPawnExitMap(this.pawn))
            {
                return;
            }
            this.pawn.ExitMap(true, CellRect.WholeMap(base.Map.BaseMap()).GetClosestEdge(this.pawn.Position));
        }
    }
}
