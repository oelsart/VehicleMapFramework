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
        public static Toil GotoVehicleEnterSpot(TargetInfo enterSpot)
        {
            Toil toil = ToilMaker.MakeToil("GotoThingOnVehicle");
            IntVec3 dest = IntVec3.Invalid;
            IntVec3 faceCell = IntVec3.Zero;
            enterSpot.Map.IsVehicleMapOf(out var vehicle);
            var baseMap = enterSpot.Map.BaseMap();
            var dist = 1;
            var vehicleOffset = IntVec3.Zero;
            toil.initAction = delegate ()
            {
                faceCell = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotationOfThing().FacingCell : enterSpot.Cell.BaseFullDirectionToInsideMap(enterSpot.Map).FacingCell;
                faceCell.y = 0;
                var basePos = enterSpot.HasThing ? enterSpot.Thing.PositionOnBaseMap() : enterSpot.Cell.OrigToVehicleMap(vehicle);
                while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                {
                    dist++;
                }
                if (toil.actor is VehiclePawn vehiclePawn) vehicleOffset = faceCell * vehiclePawn.HalfLength(); 
                dest = basePos - faceCell * dist - vehicleOffset;
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
                faceCell = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotationOfThing().FacingCell : enterSpot.Cell.BaseFullDirectionToInsideMap(enterSpot.Map).FacingCell;
                faceCell.y = 0;
                var basePos = enterSpot.HasThing ? enterSpot.Thing.PositionOnBaseMap() : enterSpot.Cell.OrigToVehicleMap(vehicle);
                if (toil.actor is VehiclePawn vehiclePawn) vehicleOffset = faceCell * vehiclePawn.HalfLength();
                var curDest = basePos - faceCell * dist - vehicleOffset;
                if (dest != curDest)
                {
                    dest = curDest;
                    toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
                }
            };
            toil.FailOn(() =>
            {
                return !enterSpot.IsValid || baseMap != toil.actor.BaseMap();
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
                bool validator(Thing t) => t.Spawned && t.def == actor.carryTracker.CarriedThing.def && t.CanStackWith(actor.carryTracker.CarriedThing) && !t.IsForbidden(actor) && t.IsSociallyProper(actor, false, true) && (takeFromValidStorage || !t.IsInValidStorage()) && (storeCellInd == TargetIndex.None || curJob.GetTarget(storeCellInd).Cell.IsValidStorageFor(destMap, t)) && actor.CanReserve(t, destMap, 1, -1, null, false) && (extraValidator == null || extraValidator(t));
                Thing thing = GenClosestOnVehicle.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways), PathEndMode.ClosestTouch, TraverseParms.For(actor, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 8f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
                if (thing != null)
                {
                    curJob.SetTarget(haulableInd, thing);
                    actor.jobs.curDriver.JumpToToil(getHaulTargetToil);
                }
            };
            return toil;
        }

        public static IEnumerable<Toil> GotoTargetMap(JobDriverAcrossMaps driver, TargetInfo exitSpot, TargetInfo enterSpot)
        {
            if (exitSpot.IsValid)
            {
                //あれ？もうexitSpotから出た後じゃない？ジャンプしよ
                var afterExitMap = Toils_General.Label();
                yield return Toils_Jump.JumpIf(afterExitMap, () =>
                {
                    return driver.pawn.Map != exitSpot.Map;
                });

                //exitSpotの場所まで行く。vehicleの場合はvehicleの長さ分手前に目的地を指定
                var vehiclePawn = driver.pawn as VehiclePawn;
                var vehicleOffset2 = vehiclePawn != null ? (exitSpot.HasThing ? exitSpot.Thing.Rotation.FacingCell : exitSpot.Cell.DirectionToInsideMap(exitSpot.Map).FacingCell) * vehiclePawn.HalfLength() : IntVec3.Zero;
                var toil = Toils_Goto.GotoCell(exitSpot.Cell + vehicleOffset2, PathEndMode.OnCell);
                yield return toil;

                //ドアがあれば開ける
                Building_Door door;
                if ((door = exitSpot.Cell.GetDoor(exitSpot.Map)) != null)
                {
                    var waitOpen = Toils_General.Wait(door.TicksToOpenNow);
                    waitOpen.initAction = (Action)Delegate.Combine(waitOpen.initAction, new Action(() =>
                    {
                        door.StartManualOpenBy(waitOpen.actor);
                    }));
                    yield return waitOpen;
                }

                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = driver.pawn.TicksPerMoveCardinal * 3f;
                if (!exitSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var offset = Vector3.zero;
                var initTick = 0;
                var faceCell = IntVec3.Zero;
                var dist = 1;
                exitSpot.Map.IsVehicleMapOf(out var vehicle);
                var baseMap = exitSpot.Map.BaseMap();
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    initTick = GenTicks.TicksGame;
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.OrigToVehicleMap(vehicle);
                    faceCell = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotationOfThing().FacingCell : exitSpot.Cell.BaseFullDirectionToInsideMap(exitSpot.Map).FacingCell;
                    faceCell.y = 0;
                    var rot = exitSpot.HasThing ? exitSpot.Thing.Rotation : exitSpot.Cell.DirectionToInsideMap(exitSpot.Map);
                    Rot4 baseRot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotationOfThing() : exitSpot.Cell.BaseFullDirectionToInsideMap(exitSpot.Map);
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    ticks *= dist + (vehiclePawn != null ? vehiclePawn.HalfLength() * 2 : 0);
                    driver.ticksLeftThisToil = (int)ticks;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.SetPositionDirect(exitSpot.Cell + vehicleOffset2);
                        vehiclePawn.FullRotation = rot.Opposite;
                    }
                    else
                    {
                        toil2.actor.Rotation = baseRot.Opposite;
                    }
                    var faceCell2 = rot.FacingCell;
                    faceCell2.y = 0;
                    var initPos = GenThing.TrueCenter(basePos - faceCell * dist - (vehiclePawn != null ? faceCell * vehiclePawn.HalfLength() : IntVec3.Zero), baseRot.Opposite, driver.pawn.def.size, 0f);
                    if (driver.pawn.def.size.x % 2 == 0 &&
                    ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                    (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                    vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot.IsHorizontal ? Vector3.back : Vector3.right;
                    }
                    var initPos2 = GenThing.TrueCenter(exitSpot.Cell + (vehiclePawn != null ? faceCell2 * vehiclePawn.HalfLength() : IntVec3.Zero), rot.Opposite, driver.pawn.def.size, 0f).OrigToVehicleMap(vehicle).WithY(0f);
                    offset = initPos - initPos2;
                }));

                toil2.tickAction = () =>
                {
                    driver.drawOffset = offset.RotatedBy(-vehicle.FullRotation.AsAngle) * ((GenTicks.TicksGame - initTick) / ticks);
                    door?.StartManualOpenBy(toil2.actor);
                };
                yield return toil2;

                //デスポーン後目的地のマップにリスポーン。スポーン地の再計算時にそこが埋まってたらとりあえず失敗に
                var toil3 = ToilMaker.MakeToil("Exit Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.FailOn(() =>
                {
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.OrigToVehicleMap(vehicle);
                    var rot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotationOfThing() : exitSpot.Cell.BaseFullDirectionToInsideMap(exitSpot.Map);
                    faceCell = rot.FacingCell;
                    faceCell.y = 0;
                    var vehicleOffset = vehiclePawn != null ? faceCell * vehiclePawn.HalfLength() : IntVec3.Zero;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    return vehiclePawn != null ? !vehiclePawn.CellRectStandable(baseMap, basePos - faceCell * dist - vehicleOffset, rot.Opposite) : !(basePos - faceCell * dist).Standable(baseMap);
                });
                toil3.initAction = () =>
                {
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.OrigToVehicleMap(vehicle);
                    var baseRot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotationOfThing() : exitSpot.Cell.BaseFullDirectionToInsideMap(exitSpot.Map);
                    driver.drawOffset = Vector3.zero;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.DeSpawnWithoutJobClearVehicle();
                        GenSpawn.Spawn(toil3.actor, basePos - faceCell * dist - faceCell * vehiclePawn.HalfLength(), exitSpot.Map.BaseMap(), baseRot.Opposite, WipeMode.Vanish);
                    }
                    else
                    {
                        toil3.actor.DeSpawnWithoutJobClear();
                        GenSpawn.Spawn(toil3.actor, basePos - faceCell * dist, exitSpot.Map.BaseMap(), baseRot.Opposite, WipeMode.Vanish);
                    }
                };
                yield return toil3;
                yield return afterExitMap;
            }
            if (enterSpot.IsValid)
            {
                //あれ？もうenterSpotのマップに居ない？ジャンプしよ
                var afterEnterMap = Toils_General.Label();
                yield return Toils_Jump.JumpIf(afterEnterMap, () =>
                {
                    return driver.pawn.Map == enterSpot.Map;
                });

                //enterSpotの手前の場所まで行く。vehicleの長さ分のオフセットはメソッド内でやっている
                var vehiclePawn = driver.pawn as VehiclePawn;
                var toil = ToilsAcrossMaps.GotoVehicleEnterSpot(enterSpot);
                yield return toil;

                //ドアがあれば開ける
                Building_Door door;
                if ((door = enterSpot.Cell.GetDoor(enterSpot.Map)) != null)
                {
                    var waitOpen = Toils_General.Wait(door.TicksToOpenNow);
                    waitOpen.initAction = (Action)Delegate.Combine(waitOpen.initAction, new Action(() =>
                    {
                        door.StartManualOpenBy(waitOpen.actor);
                    }));
                    yield return waitOpen;
                }

                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = driver.pawn.TicksPerMoveCardinal * 3f;
                if (!enterSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var initPos3 = Vector3.zero;
                var offset = Vector3.zero;
                var initTick = 0;
                var faceCell = IntVec3.Zero;
                var dist = 1;
                enterSpot.Map.IsVehicleMapOf(out var vehicle);
                var baseMap = enterSpot.Map.BaseMap();
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    var baseRot8 = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotationOfThing() : enterSpot.Cell.BaseFullDirectionToInsideMap(enterSpot.Map);
                    Rot4 baseRot4 = baseRot8;
                    var basePos = enterSpot.HasThing ? enterSpot.Thing.PositionOnBaseMap() : enterSpot.Cell.OrigToVehicleMap(vehicle);
                    faceCell = baseRot8.FacingCell;
                    faceCell.y = 0;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    ticks *= dist + (vehiclePawn != null ? vehiclePawn.HalfLength() * 2 : 0);
                    driver.ticksLeftThisToil = (int)ticks;
                    var vehicleOffset = vehiclePawn != null ? faceCell * vehiclePawn.HalfLength() : IntVec3.Zero;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.SetPositionDirect(basePos - faceCell * dist - vehicleOffset);
                        vehiclePawn.FullRotation = baseRot8;
                    }
                    else
                    {
                        toil2.actor.Rotation = baseRot4;
                    }
                    var rot = enterSpot.HasThing ? enterSpot.Thing.Rotation : enterSpot.Cell.DirectionToInsideMap(enterSpot.Map);
                    var faceCell2 = rot.FacingCell;
                    faceCell2.y = 0;
                    var initPos = GenThing.TrueCenter(enterSpot.Cell + (vehiclePawn != null ? faceCell2 * vehiclePawn.HalfLength() : IntVec3.Zero), rot, driver.pawn.def.size, 0f).OrigToVehicleMap(vehicle).WithY(0f);
                    if (driver.pawn.def.size.x % 2 == 0 &&
                    ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                    (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                    vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot4.IsHorizontal ? Vector3.forward : Vector3.left;
                    }
                    var initPos2 = GenThing.TrueCenter(basePos - faceCell * dist - vehicleOffset, baseRot8, driver.pawn.def.size, 0f);
                    initPos3 = enterSpot.Cell.OrigToVehicleMap(vehicle).ToVector3().WithY(0f);
                    offset = initPos - initPos2;
                    initTick = GenTicks.TicksGame;
                }));

                toil2.tickAction = () =>
                {
                    driver.drawOffset = offset * ((GenTicks.TicksGame - initTick) / ticks) + enterSpot.Cell.OrigToVehicleMap(vehicle).ToVector3().WithY(VehicleMapUtility.altitudeOffsetFull) - initPos3;
                    door?.StartManualOpenBy(toil2.actor);
                };
                yield return toil2;

                //デスポーン後目的地のマップにリスポーン。スポーン地の再計算時にそこが埋まってたらとりあえず失敗に
                var toil3 = ToilMaker.MakeToil("Enter Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    driver.drawOffset = Vector3.zero;
                    var rot = enterSpot.HasThing ? enterSpot.Thing.Rotation : enterSpot.Cell.DirectionToInsideMap(enterSpot.Map);
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.DeSpawnWithoutJobClearVehicle();
                        GenSpawn.Spawn(toil3.actor, enterSpot.Cell + rot.FacingCell * vehiclePawn.HalfLength(), enterSpot.Map, rot, WipeMode.Vanish);
                    }
                    else
                    {
                        toil3.actor.DeSpawnWithoutJobClear();
                        GenSpawn.Spawn(toil3.actor, enterSpot.Cell, enterSpot.Map, rot, WipeMode.VanishOrMoveAside);
                    }
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