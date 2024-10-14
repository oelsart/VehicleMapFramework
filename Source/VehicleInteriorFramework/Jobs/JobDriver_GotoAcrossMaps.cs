﻿using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_GotoAcrossMaps : JobDriver
    {
        public override Vector3 ForcedBodyOffset
        {
            get
            {
                return this.offset;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.job.targetB.HasThing)
            {
                this.job.targetB.Thing.Map.pawnDestinationReservationManager.Reserve(this.pawn, this.job, this.job.targetC.Cell);
            }else if (this.job.targetA.HasThing)
            {
                this.pawn.BaseMapOfThing().pawnDestinationReservationManager.Reserve(this.pawn, this.job, this.job.targetC.Cell);
            }
            else this.pawn.Map.pawnDestinationReservationManager.Reserve(this.pawn, this.job, this.job.targetC.Cell);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (job.targetA.HasThing)
            {
                var enterSpot = this.job.targetA.Thing;
                var toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
                yield return toil;

                var toil2 = Toils_General.Wait(90);
                toil2.handlingFacing = true;
                var initTick = 0;
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    initTick = GenTicks.TicksGame;
                    var curPos = (enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell).ToVector3Shifted();
                }));

                toil2.tickAction = () =>
                {
                    var curPos = (enterSpot.PositionOnBaseMap() - enterSpot.Rotation.FacingCell).ToVector3Shifted();
                    this.offset = (curPos - enterSpot.DrawPos) * ((GenTicks.TicksGame - initTick) / 90f);
                    this.pawn.Rotation = enterSpot.BaseRotationOfThing();
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Exit Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    var drafted = this.pawn.Drafted;
                    var selected = Find.Selector.IsSelected(this.pawn);
                    this.pawn.DeSpawnWithoutJobClear();
                    GenSpawn.Spawn(this.pawn, (enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell), enterSpot.BaseMapOfThing(), WipeMode.VanishOrMoveAside);
                    if (this.pawn.drafter != null) this.pawn.drafter.Drafted = drafted;
                    if (selected) Find.Selector.SelectedObjects.Add(this.pawn);
                };
                yield return toil3;
            }
            if (job.targetB.HasThing)
            {
                var enterSpot = this.job.targetB.Thing;
                var toil = ToilsAcrossMaps.GotoVehicleEnterSpot(job.targetB.Thing);

                yield return toil;

                var toil2 = Toils_General.Wait(90);
                toil2.handlingFacing = true;
                Vector3 initPos = Vector3.zero;
                Vector3 initPos2 = Vector3.zero;
                var initTick = 0;
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    initPos = (enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell).ToVector3Shifted();
                    initPos2 = enterSpot.DrawPos;
                    initTick = GenTicks.TicksGame;
                }));

                toil2.tickAction = () =>
                {
                    this.offset = (initPos2 - initPos) * ((GenTicks.TicksGame - initTick) / 90f) + initPos2 - enterSpot.DrawPos;
                    this.pawn.Rotation = enterSpot.BaseRotationOfThing();
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Enter Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    var drafted = this.pawn.Drafted;
                    var selected = Find.Selector.IsSelected(this.pawn);
                    this.pawn.DeSpawnWithoutJobClear();
                    GenSpawn.Spawn(this.pawn, enterSpot.Position, enterSpot.Map, WipeMode.VanishOrMoveAside);
                    if (this.pawn.drafter != null) this.pawn.drafter.Drafted = drafted;
                    if (selected) Find.Selector.SelectedObjects.Add(this.pawn);
                };
                yield return toil3;
            }
            if (job.targetC.IsValid)
            {
                var toil = Toils_Goto.Goto(TargetIndex.C, PathEndMode.OnCell);
                yield return toil;
            }
        }

        private Vector3 offset;
    }
}