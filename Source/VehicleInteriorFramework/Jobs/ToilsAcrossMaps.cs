using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class ToilsAcrossMaps
    {
        public static Toil GotoVehicleEnterSpot(Thing enterSpot)
        {
            Toil toil = ToilMaker.MakeToil("GotoThingOnVehicle");
            IntVec3 dest = IntVec3.Invalid;
            IntVec3 faceCell = IntVec3.Zero;
            enterSpot.IsOnVehicleMapOf(out var vehicle);
            var dist = 1;
            toil.initAction = delegate ()
            {
                faceCell = enterSpot.BaseFullRotationOfThing().FacingCell;
                faceCell.y = 0;
                while ((enterSpot.PositionOnBaseMap() - faceCell * dist).GetThingList(enterSpot.BaseMap()).Contains(vehicle))
                {
                    dist++;
                }
                dest = enterSpot.PositionOnBaseMap() - faceCell * dist;
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
                var curDest = enterSpot.PositionOnBaseMap() - faceCell * dist;
                if (dest != curDest)
                {
                    dest = curDest;
                    toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
                }
            };
            toil.FailOn(() =>
            {
                return enterSpot == null || !enterSpot.Spawned || enterSpot.BaseMap() != toil.actor.BaseMap();
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

                var toil2 = Toils_General.Wait(40);
                toil2.handlingFacing = true;
                var initTick = 0;
                var faceCell = IntVec3.Zero;
                var dist = 1;
                exitSpot.Thing.IsOnVehicleMapOf(out var vehicle);
                var baseMap = exitSpot.Thing.BaseMap();
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    initTick = GenTicks.TicksGame;
                    var basePos = exitSpot.Thing.PositionOnBaseMap();
                    faceCell = exitSpot.Thing.BaseFullRotationOfThing().FacingCell;
                    faceCell.y = 0;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    driver.ticksLeftThisToil *= dist;
                }));

                toil2.tickAction = () =>
                {
                    var curPos = (exitSpot.Thing.PositionOnBaseMap() - exitSpot.Thing.Rotation.FacingCell * dist).ToVector3();
                    driver.drawOffset = (curPos - exitSpot.Thing.DrawPos.WithY(0f)) * ((GenTicks.TicksGame - initTick) / (60f * dist));
                    toil2.actor.Rotation = exitSpot.Thing.BaseRotationOfThing().Opposite;
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Exit Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.FailOn(() =>
                {
                    var basePos = exitSpot.Thing.PositionOnBaseMap();
                    faceCell = exitSpot.Thing.BaseFullRotationOfThing().FacingCell;
                    faceCell.y = 0;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    return !(basePos - faceCell * dist).Standable(baseMap);
                });
                toil3.initAction = () =>
                {
                    driver.drawOffset = Vector3.zero;
                    toil3.actor.DeSpawnWithoutJobClear();
                    GenSpawn.Spawn(toil3.actor, (exitSpot.Thing.PositionOnBaseMap() - faceCell * dist), exitSpot.Thing.BaseMap(), WipeMode.Vanish);
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

                Building_Door door;
                if ((door = enterSpot.Cell.GetDoor(enterSpot.Thing.Map)) != null)
                {
                    var waitOpen = Toils_General.Wait(door.TicksToOpenNow);
                    waitOpen.initAction = (Action)Delegate.Combine(waitOpen.initAction, new Action(() =>
                    {
                        door.StartManualOpenBy(waitOpen.actor);
                    }));
                    yield return waitOpen;
                }

                var toil2 = Toils_General.Wait(40);
                toil2.handlingFacing = true;
                Vector3 initPos = Vector3.zero;
                Vector3 initPos2 = Vector3.zero;
                var initTick = 0;
                var faceCell = IntVec3.Zero;
                var dist = 1;
                enterSpot.Thing.IsOnVehicleMapOf(out var vehicle);
                var baseMap = enterSpot.Thing.BaseMap();
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    var basePos = enterSpot.Thing.PositionOnBaseMap();
                    faceCell = enterSpot.Thing.BaseFullRotationOfThing().FacingCell;
                    faceCell.y = 0;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    driver.ticksLeftThisToil *= dist;
                    initPos = (basePos - faceCell * dist).ToVector3Shifted();
                    initPos2 = enterSpot.Thing.DrawPos.WithY(0f);
                    initTick = GenTicks.TicksGame;
                }));

                toil2.tickAction = () =>
                {
                    driver.drawOffset = (initPos2 - initPos) * ((GenTicks.TicksGame - initTick) / (40f * dist)) + enterSpot.Thing.DrawPos.WithY(VehicleMapUtility.altitudeOffsetFull) - initPos2;
                    toil2.actor.Rotation = enterSpot.Thing.BaseRotationOfThing();
                    door?.StartManualOpenBy(toil2.actor);
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Enter Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    driver.drawOffset = Vector3.zero;
                    toil3.actor.DeSpawnWithoutJobClear();
                    GenSpawn.Spawn(toil3.actor, enterSpot.Thing.Position, enterSpot.Thing.Map, WipeMode.VanishOrMoveAside);
                };
                yield return toil3;
                yield return afterEnterMap;
            }
        }

        public static T FailOnForbidden<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                Pawn actor = f.GetActor();
                if (actor.Faction != Faction.OfPlayer)
                {
                    return JobCondition.Ongoing;
                }
                if (actor.jobs.curJob.ignoreForbidden)
                {
                    return JobCondition.Ongoing;
                }
                Thing thing = actor.jobs.curJob.GetTarget(ind).Thing;
                if (thing == null)
                {
                    return JobCondition.Ongoing;
                }
                if (thing.IsForbidden(actor))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

        public static T FailOnDespawnedOrNull<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                if (f.GetActor().jobs.curJob.GetTarget(ind).DespawnedOrNull(f.GetActor()))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

        public static bool DespawnedOrNull(this LocalTargetInfo target, Pawn actor)
        {
            Thing thing = target.Thing;
            return (thing != null || !target.IsValid) && (thing == null || !thing.Spawned || thing.BaseMap() != actor.BaseMap());
        }

        public static T FailOnSomeonePhysicallyInteracting<T>(this T f, TargetIndex ind, Map targMap) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                Pawn actor = f.GetActor();
                Thing thing = actor.jobs.curJob.GetTarget(ind).Thing;
                if (thing != null && targMap.physicalInteractionReservationManager.IsReserved(thing) && !targMap.physicalInteractionReservationManager.IsReservedBy(actor, thing))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }
    }
}