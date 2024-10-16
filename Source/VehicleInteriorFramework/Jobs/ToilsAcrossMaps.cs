using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class ToilsAcrossMaps
    {
        public static Toil GotoVehicleEnterSpot(Thing enterSpot)
        {
            Toil toil = ToilMaker.MakeToil("GotoThingOnVehicle");
            IntVec3 dest = enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell;
            toil.initAction = delegate ()
            {
                if (toil.actor.Position == dest)
                {
                    toil.actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            toil.tickAction = () =>
            {
                var curDest = enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell;
                if (dest != curDest)
                {
                    dest = curDest;
                    toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
                }
            };
            toil.FailOn(() =>
            {
                return enterSpot == null || !enterSpot.Spawned || enterSpot.BaseMapOfThing() != toil.actor.BaseMapOfThing();
            });
            return toil;
        }

        public static Toil Reserve(TargetIndex ind, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool ignoreOtherReservations = false)
        {
            Toil toil = ToilMaker.MakeToil("Reserve");
            toil.initAction = delegate ()
            {
                var target = toil.actor.jobs.curJob.GetTarget(ind);
                toil.FailOn(() => !target.HasThing);
                if (!toil.actor.Reserve(target.Thing.Map, target, toil.actor.CurJob, maxPawns, stackCount, layer, false, ignoreOtherReservations))
                {
                    toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.atomicWithPrevious = true;
            return toil;
        }

        public static Toil CheckForGetOpportunityDuplicate(Toil getHaulTargetToil, TargetIndex haulableInd, TargetIndex storeCellInd, Map destMap, bool takeFromValidStorage = false, Predicate<Thing> extraValidator = null)
        {
            Toil toil = ToilMaker.MakeToil("CheckForGetOpportunityDuplicate");
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (actor.carryTracker.CarriedThing.def.stackLimit == 1)
                {
                    return;
                }
                if (actor.carryTracker.Full)
                {
                    return;
                }
                if (curJob.count <= 0)
                {
                    return;
                }
                Predicate<Thing> validator = (Thing t) => t.Spawned && t.def == actor.carryTracker.CarriedThing.def && t.CanStackWith(actor.carryTracker.CarriedThing) && !t.IsForbidden(actor) && t.IsSociallyProper(actor, false, true) && (takeFromValidStorage || !t.IsInValidStorage()) && (storeCellInd == TargetIndex.None || curJob.GetTarget(storeCellInd).Cell.IsValidStorageFor(destMap, t)) && actor.CanReserve(t, destMap, 1, -1, null, false) && (extraValidator == null || extraValidator(t));
                Thing thing = GenClosestOnVehicle.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways), PathEndMode.ClosestTouch, TraverseParms.For(actor, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 8f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
                if (thing != null)
                {
                    curJob.SetTarget(haulableInd, thing);
                    actor.jobs.curDriver.JumpToToil(getHaulTargetToil);
                }
            };
            return toil;
        }

        public static IEnumerable<Toil> GotoTargetMap(JobDriverAcrossMaps driver, LocalTargetInfo exitSpot, LocalTargetInfo enterSpot)
        {
            if (exitSpot.HasThing)
            {
                var afterExitMap = Toils_General.Label();
                yield return Toils_Jump.JumpIf(afterExitMap, () =>
                {
                    return driver.pawn.Map != exitSpot.Thing.Map;
                });

                var toil = Toils_Goto.GotoCell(exitSpot.Cell, PathEndMode.OnCell);
                yield return toil;

                var toil2 = Toils_General.Wait(90);
                toil2.handlingFacing = true;
                var initTick = 0;
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    initTick = GenTicks.TicksGame;
                    var curPos = (exitSpot.Thing.PositionOnBaseMap() - exitSpot.Thing.BaseFullRotationOfThing().FacingCell).ToVector3Shifted();
                }));

                toil2.tickAction = () =>
                {
                    var curPos = (exitSpot.Thing.PositionOnBaseMap() - exitSpot.Thing.Rotation.FacingCell).ToVector3Shifted();
                    driver.drawOffset = (curPos - exitSpot.Thing.DrawPos.WithY(0f)) * ((GenTicks.TicksGame - initTick) / 90f);
                    toil2.actor.Rotation = exitSpot.Thing.BaseRotationOfThing();
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Exit Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    driver.drawOffset = Vector3.zero;
                    //var drafted = toil3.actor.Drafted;
                    //var selected = Find.Selector.IsSelected(toil3.actor);
                    toil3.actor.DeSpawnWithoutJobClear();
                    GenSpawn.Spawn(toil3.actor, (exitSpot.Thing.PositionOnBaseMap() - exitSpot.Thing.BaseFullRotationOfThing().FacingCell), exitSpot.Thing.BaseMapOfThing(), WipeMode.Vanish);
                    //if (toil3.actor.drafter != null) draftedInt(toil3.actor.drafter) = drafted;
                    //if (selected) Find.Selector.SelectedObjects.Add(toil3.actor);
                };
                yield return toil3;
                yield return afterExitMap;
            }
            if (enterSpot.HasThing)
            {
                var afterEnterMap = Toils_General.Label();
                yield return Toils_Jump.JumpIf(afterEnterMap, () =>
                {
                    return driver.pawn.Map == enterSpot.Thing.Map;
                });

                var toil = ToilsAcrossMaps.GotoVehicleEnterSpot(enterSpot.Thing);
                yield return toil;

                var toil2 = Toils_General.Wait(90);
                toil2.handlingFacing = true;
                Vector3 initPos = Vector3.zero;
                Vector3 initPos2 = Vector3.zero;
                var initTick = 0;
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    initPos = (enterSpot.Thing.PositionOnBaseMap() - enterSpot.Thing.BaseFullRotationOfThing().FacingCell).ToVector3Shifted();
                    initPos2 = enterSpot.Thing.DrawPos.WithY(0f);
                    initTick = GenTicks.TicksGame;
                }));

                toil2.tickAction = () =>
                {
                    driver.drawOffset = (initPos2 - initPos) * ((GenTicks.TicksGame - initTick) / 90f) + enterSpot.Thing.DrawPos - initPos2;
                    driver.drawOffset.y += VehicleMapUtility.altitudeOffsetFull;
                    toil2.actor.Rotation = enterSpot.Thing.BaseRotationOfThing();
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Enter Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    driver.drawOffset = Vector3.zero;
                    //var drafted = toil3.actor.Drafted;
                    //var selected = Find.Selector.IsSelected(toil3.actor);
                    toil3.actor.DeSpawnWithoutJobClear();
                    GenSpawn.Spawn(toil3.actor, enterSpot.Thing.Position, enterSpot.Thing.Map, WipeMode.VanishOrMoveAside);
                    //if (toil3.actor.drafter != null) draftedInt(toil3.actor.drafter) = drafted;
                    //if (selected) Find.Selector.SelectedObjects.Add(toil3.actor);
                };
                yield return toil3;
                yield return afterEnterMap;
            }
        }

        private static AccessTools.FieldRef<Pawn_DraftController, bool> draftedInt = AccessTools.FieldRefAccess<Pawn_DraftController, bool>("draftedInt");
    }
}