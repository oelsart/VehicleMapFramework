using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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
                faceCell = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotation().FacingCell : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).FacingCell;
                faceCell.y = 0;
                var basePos = enterSpot.HasThing ? enterSpot.Thing.PositionOnBaseMap() : enterSpot.Cell.ToBaseMapCoord(vehicle);
                while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                {
                    dist++;
                }
                if (enterSpot.Thing is Building_VehicleRamp) dist++;
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
                faceCell = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotation().FacingCell : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).FacingCell;
                faceCell.y = 0;
                var basePos = enterSpot.HasThing ? enterSpot.Thing.PositionOnBaseMap() : enterSpot.Cell.ToBaseMapCoord(vehicle);
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
                if (!toil.actor.Reserve(target.Thing.MapHeld, target, toil.actor.CurJob, maxPawns, stackCount, layer, false, ignoreOtherReservations))
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
            if (exitSpot.IsValid && exitSpot.Map != null)
            {
                //あれ？もうexitSpotから出た後じゃない？ジャンプしよ
                var afterExitMap = Toils_General.Label();
                yield return Toils_Jump.JumpIf(afterExitMap, () =>
                {
                    return driver.pawn.Map != exitSpot.Map;
                });

                exitSpot.Map.IsVehicleMapOf(out var vehicle);
                //exitSpotの場所まで行く。vehicleの場合はvehicleの長さ分手前に目的地を指定
                var vehiclePawn = driver.pawn as VehiclePawn;
                var vehicleOffset2 = vehiclePawn != null ? (exitSpot.HasThing ? exitSpot.Thing.Rotation.FacingCell : exitSpot.Cell.DirectionToInsideMap(vehicle).FacingCell) * vehiclePawn.HalfLength() : IntVec3.Zero;
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
                if (exitSpot.Cell.TryGetFirstThing<Building_VehicleRamp>(exitSpot.Map, out var ramp))
                {
                    var waitOpen = Toils_General.Wait(ramp.TicksToOpenNow);
                    waitOpen.initAction = (Action)Delegate.Combine(waitOpen.initAction, new Action(() =>
                    {
                        ramp.StartManualOpenBy(waitOpen.actor);
                    }));
                    yield return waitOpen;
                }

                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = driver.pawn.TicksPerMoveCardinal * 4f;
                if (!exitSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var offset = Vector3.zero;
                var initTick = 0;
                var faceCell = IntVec3.Zero;
                var dist = 1;
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    var baseMap = exitSpot.Map.BaseMap();
                    initTick = GenTicks.TicksGame;
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.ToBaseMapCoord(vehicle);
                    faceCell = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation().FacingCell : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle).FacingCell;
                    faceCell.y = 0;
                    var rot = exitSpot.HasThing ? exitSpot.Thing.Rotation : exitSpot.Cell.DirectionToInsideMap(vehicle);
                    Rot4 baseRot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation() : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    if (exitSpot.Thing is Building_VehicleRamp) dist++;
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
                    var initPos2 = GenThing.TrueCenter(exitSpot.Cell + (vehiclePawn != null ? faceCell2 * vehiclePawn.HalfLength() : IntVec3.Zero), rot.Opposite, driver.pawn.def.size, 0f).ToBaseMapCoord(vehicle).WithY(0f);
                    offset = initPos - initPos2;
                }));

                toil2.tickAction = () =>
                {
                    driver.drawOffset = offset.RotatedBy(-vehicle.FullRotation.AsAngle) * ((GenTicks.TicksGame - initTick) / ticks);
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                };

                //toil2.FailOn(() => !exitSpot.Cell.Standable(exitSpot.Map));
                yield return toil2;

                //デスポーン後目的地のマップにリスポーン。スポーン地の再計算時にそこが埋まってたらとりあえず失敗に
                var toil3 = ToilMaker.MakeToil("Exit Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.FailOn(() =>
                {
                    var baseMap = exitSpot.Map.BaseMap();
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.ToBaseMapCoord(vehicle);
                    var rot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation() : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    faceCell = rot.FacingCell;
                    faceCell.y = 0;
                    var vehicleOffset = vehiclePawn != null ? faceCell * vehiclePawn.HalfLength() : IntVec3.Zero;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    if (exitSpot.Thing is Building_VehicleRamp) dist++;
                    return vehiclePawn != null ? !vehiclePawn.CellRectStandable(baseMap, basePos - faceCell * dist - vehicleOffset, rot.Opposite) : !(basePos - faceCell * dist).Standable(baseMap);
                });
                toil3.initAction = () =>
                {
                    var baseMap = exitSpot.Map.BaseMap();
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.ToBaseMapCoord(vehicle);
                    var baseRot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation() : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    driver.drawOffset = Vector3.zero;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.DeSpawnWithoutJobClearVehicle();
                        GenSpawn.Spawn(toil3.actor, basePos - faceCell * dist - faceCell * vehiclePawn.HalfLength(), baseMap, baseRot.Opposite, WipeMode.Vanish);
                    }
                    else
                    {
                        toil3.actor.DeSpawnWithoutJobClear();
                        foreach (var ropee in toil3.actor.roping?.Ropees)
                        {
                            ropee.DeSpawnWithoutJobClear();
                        }
                        GenSpawn.Spawn(toil3.actor, basePos - faceCell * dist, baseMap, baseRot.Opposite, WipeMode.Vanish);
                        foreach (var ropee in toil3.actor.roping?.Ropees)
                        {
                            GenSpawn.Spawn(ropee, basePos - faceCell * dist, baseMap, baseRot.Opposite, WipeMode.Vanish);
                        }
                    }
                };
                yield return toil3;
                yield return afterExitMap;
            }
            if (enterSpot.IsValid && enterSpot.Map != null)
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
                if (enterSpot.Cell.TryGetFirstThing<Building_VehicleRamp>(enterSpot.Map, out var ramp))
                {
                    var waitOpen = Toils_General.Wait(ramp.TicksToOpenNow);
                    waitOpen.initAction = (Action)Delegate.Combine(waitOpen.initAction, new Action(() =>
                    {
                        ramp.StartManualOpenBy(waitOpen.actor);
                    }));
                    yield return waitOpen;
                }

                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = driver.pawn.TicksPerMoveCardinal * 4f;
                if (!enterSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var initPos3 = Vector3.zero;
                var offset = Vector3.zero;
                var initTick = 0;
                var faceCell = IntVec3.Zero;
                var dist = 1;
                enterSpot.Map.IsVehicleMapOf(out var vehicle);
                toil2.initAction = (Action)Delegate.Combine(toil2.initAction, new Action(() =>
                {
                    var baseMap = enterSpot.Map.BaseMap();
                    var baseRot8 = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotation() : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    Rot4 baseRot4 = baseRot8;
                    var basePos = enterSpot.HasThing ? enterSpot.Thing.PositionOnBaseMap() : enterSpot.Cell.ToBaseMapCoord(vehicle);
                    faceCell = baseRot8.FacingCell;
                    faceCell.y = 0;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    if (enterSpot.Thing is Building_VehicleRamp) dist++;
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
                    var rot = enterSpot.HasThing ? enterSpot.Thing.Rotation : enterSpot.Cell.DirectionToInsideMap(vehicle);
                    var faceCell2 = rot.FacingCell;
                    faceCell2.y = 0;
                    var initPos = GenThing.TrueCenter(enterSpot.Cell + (vehiclePawn != null ? faceCell2 * vehiclePawn.HalfLength() : IntVec3.Zero), rot, driver.pawn.def.size, 0f).ToBaseMapCoord(vehicle).WithY(0f);
                    if (driver.pawn.def.size.x % 2 == 0 &&
                    ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                    (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                    vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot4.IsHorizontal ? Vector3.forward : Vector3.left;
                    }
                    var initPos2 = GenThing.TrueCenter(basePos - faceCell * dist - vehicleOffset, baseRot8, driver.pawn.def.size, 0f);
                    initPos3 = enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().WithY(0f);
                    offset = initPos - initPos2;
                    initTick = GenTicks.TicksGame;
                }));

                toil2.tickAction = () =>
                {
                    driver.drawOffset = offset * ((GenTicks.TicksGame - initTick) / ticks) + enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().WithY(VehicleMapUtility.altitudeOffsetFull) - initPos3;
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                };
                yield return toil2;

                var toil3 = ToilMaker.MakeToil("Enter Vehicle Map");
                toil3.defaultCompleteMode = ToilCompleteMode.Instant;
                toil3.initAction = () =>
                {
                    driver.drawOffset = Vector3.zero;
                    var rot = enterSpot.HasThing ? enterSpot.Thing.Rotation : enterSpot.Cell.DirectionToInsideMap(vehicle);
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.DeSpawnWithoutJobClearVehicle();
                        GenSpawn.Spawn(toil3.actor, enterSpot.Cell + rot.FacingCell * vehiclePawn.HalfLength(), enterSpot.Map, rot, WipeMode.Vanish);
                    }
                    else
                    {
                        toil3.actor.DeSpawnWithoutJobClear();
                        foreach (var ropee in toil3.actor.roping?.Ropees)
                        {
                            ropee.DeSpawnWithoutJobClear();
                        }
                        GenSpawn.Spawn(toil3.actor, enterSpot.Cell, enterSpot.Map, rot, WipeMode.VanishOrMoveAside);
                        foreach (var ropee in toil3.actor.roping?.Ropees)
                        {
                            GenSpawn.Spawn(ropee, enterSpot.Cell, enterSpot.Map, rot, WipeMode.VanishOrMoveAside);
                        }
                    }
                };
                yield return toil3;
                yield return afterEnterMap;
            }
        }

        public static Toil DepositHauledThingInContainer(TargetIndex containerInd, TargetIndex reserveForContainerInd, Action onDeposited = null)
        {
            Toil toil = ToilMaker.MakeToil("DepositHauledThingInContainer");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (actor.carryTracker.CarriedThing == null)
                {
                    Log.Error(string.Concat(actor, " tried to place hauled thing in container but is not hauling anything."));
                }
                else
                {
                    Thing thing = curJob.GetTarget(containerInd).Thing;
                    ThingOwner thingOwner = thing.TryGetInnerInteractableThingOwner();
                    if (thingOwner != null)
                    {
                        int num = actor.carryTracker.CarriedThing.stackCount;
                        if (thing is IHaulEnroute haulEnroute)
                        {
                            ThingDef def = actor.carryTracker.CarriedThing.def;
                            num = Mathf.Min(haulEnroute.GetSpaceRemainingWithEnroute(def, actor), num);
                            if (reserveForContainerInd != 0)
                            {
                                Thing thing2 = curJob.GetTarget(reserveForContainerInd).Thing;
                                if (!thing2.DestroyedOrNull() && thing2 != haulEnroute && thing2 is IHaulEnroute enroute)
                                {
                                    int spaceRemainingWithEnroute = enroute.GetSpaceRemainingWithEnroute(def, actor);
                                    num = Mathf.Min(num, actor.carryTracker.CarriedThing.stackCount - spaceRemainingWithEnroute);
                                }
                            }
                        }

                        Thing carriedThing = actor.carryTracker.CarriedThing;
                        int num2 = actor.carryTracker.innerContainer.TryTransferToContainer(carriedThing, thingOwner, num);
                        if (num2 != 0)
                        {
                            if (thing is IHaulEnroute container)
                            {
                                thing.Map.enrouteManager.ReleaseFor(container, actor);
                            }

                            if (thing is INotifyHauledTo notifyHauledTo)
                            {
                                notifyHauledTo.Notify_HauledTo(actor, carriedThing, num2);
                            }

                            if (thing is ThingWithComps thingWithComps)
                            {
                                foreach (ThingComp allComp in thingWithComps.AllComps)
                                {
                                    if (allComp is INotifyHauledTo notifyHauledTo2)
                                    {
                                        notifyHauledTo2.Notify_HauledTo(actor, carriedThing, num2);
                                    }
                                }
                            }

                            if (curJob.def == JobDefOf.DoBill || curJob.def == VMF_DefOf.VMF_DoBillAcrossMaps)
                            {
                                HaulAIUtility.UpdateJobWithPlacedThings(curJob, carriedThing, num2);
                            }

                            onDeposited?.Invoke();
                        }
                    }
                    else if (curJob.GetTarget(containerInd).Thing.def.Minifiable)
                    {
                        actor.carryTracker.innerContainer.ClearAndDestroyContents();
                    }
                    else
                    {
                        Log.Error("Could not deposit hauled thing in container: " + curJob.GetTarget(containerInd).Thing);
                    }
                }
            };
            return toil;
        }

        public static Toil SetTargetToIngredientPlaceCell(TargetIndex facilityInd, TargetIndex carryItemInd, TargetIndex cellTargetInd)
        {
            Toil toil = ToilMaker.MakeToil("SetTargetToIngredientPlaceCell");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(carryItemInd).Thing;
                IntVec3 intVec = IntVec3.Invalid;
                foreach (IntVec3 item in IngredientPlaceCellsInOrder(curJob.GetTarget(facilityInd).Thing))
                {
                    if (!intVec.IsValid)
                    {
                        intVec = item;
                    }

                    bool flag = false;
                    List<Thing> list = curJob.GetTarget(facilityInd).Thing.Map.thingGrid.ThingsListAt(item);
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].def.category == ThingCategory.Item && (!list[i].CanStackWith(thing) || list[i].stackCount == list[i].def.stackLimit))
                        {
                            flag = true;
                            break;
                        }
                    }

                    if (!flag)
                    {
                        curJob.SetTarget(cellTargetInd, item);
                        return;
                    }
                }

                curJob.SetTarget(cellTargetInd, intVec);
            };
            return toil;
        }

        private static readonly List<IntVec3> yieldedIngPlaceCells = new List<IntVec3>();

        private static IEnumerable<IntVec3> IngredientPlaceCellsInOrder(Thing destination)
        {
            yieldedIngPlaceCells.Clear();
            try
            {
                IntVec3 interactCell = destination.Position;
                if (destination is IBillGiver billGiver)
                {
                    interactCell = ((Thing)billGiver).InteractionCell;
                    foreach (IntVec3 item in billGiver.IngredientStackCells.OrderBy((IntVec3 c) => (c - interactCell).LengthHorizontalSquared))
                    {
                        yieldedIngPlaceCells.Add(item);
                        yield return item;
                    }
                }

                for (int i = 0; i < 200; i++)
                {
                    IntVec3 intVec = interactCell + GenRadial.RadialPattern[i];
                    if (!yieldedIngPlaceCells.Contains(intVec))
                    {
                        Building edifice = intVec.GetEdifice(destination.Map);
                        if (edifice == null || edifice.def.passability != Traversability.Impassable || edifice.def.surfaceType != 0)
                        {
                            yield return intVec;
                        }
                    }
                }
            }
            finally
            {
                yieldedIngPlaceCells.Clear();
            }
        }

        public static Toil PlaceHauledThingInCell(TargetIndex billGiverInd, TargetIndex cellInd, Toil nextToilOnPlaceFailOrIncomplete , bool storageMode, bool tryStoreInSameStorageIfSpotCantHoldWholeStack = false)
        {
            Toil toil = ToilMaker.MakeToil("PlaceHauledThingInCell");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                var ingredient = curJob.GetTarget(billGiverInd).Thing;
                IntVec3 cell = curJob.GetTarget(cellInd).Cell;
                if (actor.carryTracker.CarriedThing == null)
                {
                    Log.Error(string.Concat(actor, " tried to place hauled thing in cell but is not hauling anything."));
                }
                else
                {
                    SlotGroup slotGroup = ingredient.Map.haulDestinationManager.SlotGroupAt(cell);
                    if (slotGroup != null && slotGroup.Settings.AllowedToAccept(actor.carryTracker.CarriedThing))
                    {
                        ingredient.Map.designationManager.TryRemoveDesignationOn(actor.carryTracker.CarriedThing, DesignationDefOf.Haul);
                    }

                    Action<Thing, int> placedAction = null;
                    if (curJob.def == JobDefOf.DoBill || curJob.def == JobDefOf.RecolorApparel || curJob.def == JobDefOf.RefuelAtomic || curJob.def == JobDefOf.RearmTurretAtomic ||
                    curJob.def == VMF_DefOf.VMF_DoBillAcrossMaps || curJob.def == VMF_DefOf.VMF_RefuelAcrossMaps)
                    {
                        placedAction = delegate (Thing th, int added)
                        {
                            HaulAIUtility.UpdateJobWithPlacedThings(curJob, th, added);
                        };
                    }

                    if (!actor.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _, placedAction))
                    {
                        if (storageMode)
                        {
                            if (nextToilOnPlaceFailOrIncomplete != null && ((tryStoreInSameStorageIfSpotCantHoldWholeStack && StoreUtility.TryFindBestBetterStoreCellForIn(actor.carryTracker.CarriedThing, actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetSlotGroup(), out var foundCell)) || StoreUtility.TryFindBestBetterStoreCellFor(actor.carryTracker.CarriedThing, actor, actor.Map, StoragePriority.Unstored, actor.Faction, out foundCell)))
                            {
                                if (actor.CanReserve(foundCell))
                                {
                                    actor.Reserve(foundCell, actor.CurJob);
                                }

                                actor.CurJob.SetTarget(cellInd, foundCell);
                                actor.jobs.curDriver.JumpToToil(nextToilOnPlaceFailOrIncomplete);
                            }
                            else if (HaulAIUtility.CanHaulAside(actor, actor.carryTracker.CarriedThing, out IntVec3 storeCell))
                            {
                                curJob.SetTarget(cellInd, storeCell);
                                curJob.count = int.MaxValue;
                                curJob.haulOpportunisticDuplicates = false;
                                curJob.haulMode = HaulMode.ToCellNonStorage;
                                actor.jobs.curDriver.JumpToToil(nextToilOnPlaceFailOrIncomplete);
                            }
                            else
                            {
                                Log.Warning($"Incomplete haul for {actor}: Could not find anywhere to put {actor.carryTracker.CarriedThing} near {actor.Position}. Destroying. This should be very uncommon!");
                                actor.carryTracker.CarriedThing.Destroy();
                            }
                        }
                        else if (nextToilOnPlaceFailOrIncomplete != null)
                        {
                            actor.jobs.curDriver.JumpToToil(nextToilOnPlaceFailOrIncomplete);
                        }
                    }
                }
            };
            return toil;
        }

        public static Toil DoRecipeWork()
        {
            Toil toil = ToilMaker.MakeToil("DoRecipeWork");
            toil.initAction = delegate
            {
                Pawn actor3 = toil.actor;
                Job curJob3 = actor3.jobs.curJob;
                JobDriver_DoBillAcrossMaps jobDriver_DoBill2 = (JobDriver_DoBillAcrossMaps)actor3.jobs.curDriver;
                Thing thing3 = curJob3.GetTarget(TargetIndex.B).Thing;
                UnfinishedThing unfinishedThing2 = thing3 as UnfinishedThing;
                _ = curJob3.GetTarget(TargetIndex.A).Thing.def.building;
                if (unfinishedThing2 != null && unfinishedThing2.Initialized)
                {
                    jobDriver_DoBill2.workLeft = unfinishedThing2.workLeft;
                }
                else
                {
                    jobDriver_DoBill2.workLeft = curJob3.bill.GetWorkAmount(thing3);
                    if (unfinishedThing2 != null)
                    {
                        if (unfinishedThing2.debugCompleted)
                        {
                            unfinishedThing2.workLeft = (jobDriver_DoBill2.workLeft = 0f);
                        }
                        else
                        {
                            unfinishedThing2.workLeft = jobDriver_DoBill2.workLeft;
                        }
                    }
                }

                jobDriver_DoBill2.billStartTick = Find.TickManager.TicksGame;
                jobDriver_DoBill2.ticksSpentDoingRecipeWork = 0;
                curJob3.bill.Notify_BillWorkStarted(actor3);
            };
            toil.tickAction = delegate
            {
                Pawn actor2 = toil.actor;
                Job curJob2 = actor2.jobs.curJob;
                JobDriver_DoBillAcrossMaps jobDriver_DoBill = (JobDriver_DoBillAcrossMaps)actor2.jobs.curDriver;
                UnfinishedThing unfinishedThing = curJob2.GetTarget(TargetIndex.B).Thing as UnfinishedThing;
                if (unfinishedThing != null && unfinishedThing.Destroyed)
                {
                    actor2.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                else
                {
                    jobDriver_DoBill.ticksSpentDoingRecipeWork++;
                    curJob2.bill.Notify_PawnDidWork(actor2);
                    if (toil.actor.CurJob.GetTarget(TargetIndex.A).Thing is IBillGiverWithTickAction billGiverWithTickAction)
                    {
                        billGiverWithTickAction.UsedThisTick();
                    }

                    if (curJob2.RecipeDef.workSkill != null && curJob2.RecipeDef.UsesUnfinishedThing && actor2.skills != null)
                    {
                        actor2.skills.Learn(curJob2.RecipeDef.workSkill, 0.1f * curJob2.RecipeDef.workSkillLearnFactor);
                    }

                    float num2 = ((curJob2.RecipeDef.workSpeedStat == null) ? 1f : actor2.GetStatValue(curJob2.RecipeDef.workSpeedStat));
                    if (curJob2.RecipeDef.workTableSpeedStat != null && jobDriver_DoBill.BillGiver is Building_WorkTable thing2)
                    {
                        num2 *= thing2.GetStatValue(curJob2.RecipeDef.workTableSpeedStat);
                    }

                    if (DebugSettings.fastCrafting)
                    {
                        num2 *= 30f;
                    }

                    jobDriver_DoBill.workLeft -= num2;
                    if (unfinishedThing != null)
                    {
                        if (unfinishedThing.debugCompleted)
                        {
                            unfinishedThing.workLeft = (jobDriver_DoBill.workLeft = 0f);
                        }
                        else
                        {
                            unfinishedThing.workLeft = jobDriver_DoBill.workLeft;
                        }
                    }

                    actor2.GainComfortFromCellIfPossible(chairsOnly: true);
                    if (jobDriver_DoBill.workLeft <= 0f)
                    {
                        curJob2.bill.Notify_BillWorkFinished(actor2);
                        jobDriver_DoBill.ReadyForNextToil();
                    }
                    else if (curJob2.bill.recipe.UsesUnfinishedThing)
                    {
                        int num3 = Find.TickManager.TicksGame - jobDriver_DoBill.billStartTick;
                        if (num3 >= 3000 && num3 % 1000 == 0)
                        {
                            actor2.jobs.CheckForJobOverride();
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(() => toil.actor.CurJob.bill.recipe.effectWorking, TargetIndex.A);
            toil.PlaySustainerOrSound(() => toil.actor.CurJob.bill.recipe.soundWorking);
            toil.WithProgressBar(TargetIndex.A, delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.CurJob;
                Thing thing = curJob.GetTarget(TargetIndex.B).Thing;
                float workLeft = ((JobDriver_DoBillAcrossMaps)actor.jobs.curDriver).workLeft;
                float num = ((curJob.bill is Bill_Mech bill_Mech && bill_Mech.State == FormingState.Formed) ? 300f : curJob.bill.recipe.WorkAmountTotal(thing));
                return 1f - workLeft / num;
            });
            toil.FailOn((Func<bool>)delegate
            {
                RecipeDef recipeDef = toil.actor.CurJob.RecipeDef;
                if (recipeDef != null && recipeDef.interruptIfIngredientIsRotting)
                {
                    LocalTargetInfo target = toil.actor.CurJob.GetTarget(TargetIndex.B);
                    if (target.HasThing && (int)target.Thing.GetRotStage() > 0)
                    {
                        return true;
                    }
                }

                return toil.actor.CurJob.bill.suspended;
            });
            toil.activeSkill = () => toil.actor.CurJob.bill.recipe.workSkill;
            return toil;
        }

        public static Toil FinishRecipeAndStartStoringProduct(TargetIndex productIndex = TargetIndex.A)
        {    
            List<Thing> CalculateIngredients(Job job, Pawn actor)
            {
                if (job.GetTarget(TargetIndex.B).Thing is UnfinishedThing unfinishedThing)
                {
                    List<Thing> ingredients = unfinishedThing.ingredients;
                    job.RecipeDef.Worker.ConsumeIngredient(unfinishedThing, job.RecipeDef, actor.Map);
                    job.placedThings = null;
                    return ingredients;
                }

                List<Thing> list = new List<Thing>();
                if (job.placedThings != null)
                {
                    for (int i = 0; i < job.placedThings.Count; i++)
                    {
                        if (job.placedThings[i].Count <= 0)
                        {
                            Log.Error(string.Concat("PlacedThing ", job.placedThings[i], " with count ", job.placedThings[i].Count, " for job ", job));
                            continue;
                        }

                        Thing thing = ((job.placedThings[i].Count >= job.placedThings[i].thing.stackCount) ? job.placedThings[i].thing : job.placedThings[i].thing.SplitOff(job.placedThings[i].Count));
                        job.placedThings[i].Count = 0;
                        if (list.Contains(thing))
                        {
                            Log.Error("Tried to add ingredient from job placed targets twice: " + thing);
                            continue;
                        }

                        list.Add(thing);
                        if (job.RecipeDef.autoStripCorpses && thing is IStrippable strippable && strippable.AnythingToStrip())
                        {
                            strippable.Strip();
                        }
                    }
                }

                job.placedThings = null;
                return list;

            }

            Thing CalculateDominantIngredient(Job job, List<Thing> ingredients)
            {
                UnfinishedThing uft = job.GetTarget(TargetIndex.B).Thing as UnfinishedThing;
                if (uft != null && uft.def.MadeFromStuff)
                {
                    return uft.ingredients.First((Thing ing) => ing.def == uft.Stuff);
                }

                if (!ingredients.NullOrEmpty())
                {
                    RecipeDef recipeDef = job.RecipeDef;
                    if (recipeDef.productHasIngredientStuff)
                    {
                        return ingredients[0];
                    }

                    if (recipeDef.products.Any((ThingDefCountClass x) => x.thingDef.MadeFromStuff) || (recipeDef.unfinishedThingDef != null && recipeDef.unfinishedThingDef.MadeFromStuff))
                    {
                        return ingredients.Where((Thing x) => x.def.IsStuff).RandomElementByWeight((Thing x) => x.stackCount);
                    }

                    return ingredients.RandomElementByWeight((Thing x) => x.stackCount);
                }

                return null;
            }

            void ConsumeIngredients(List<Thing> ingredients, RecipeDef recipe, Map map)
            {
                for (int i = 0; i < ingredients.Count; i++)
                {
                    recipe.Worker.ConsumeIngredient(ingredients[i], recipe, map);
                }
            }

            Toil toil = ToilMaker.MakeToil("FinishRecipeAndStartStoringProduct");
            toil.AddFinishAction(delegate
            {
                if (toil.actor.jobs.curJob.bill is Bill_Production bill_Production && bill_Production.repeatMode == BillRepeatModeDefOf.TargetCount)
                {
                    toil.actor.Map.resourceCounter.UpdateResourceCounts();
                }
            });
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver_DoBillAcrossMaps jobDriver_DoBill = (JobDriver_DoBillAcrossMaps)actor.jobs.curDriver;
                if (curJob.RecipeDef.workSkill != null && !curJob.RecipeDef.UsesUnfinishedThing && actor.skills != null)
                {
                    float xp = (float)jobDriver_DoBill.ticksSpentDoingRecipeWork * 0.1f * curJob.RecipeDef.workSkillLearnFactor;
                    actor.skills.GetSkill(curJob.RecipeDef.workSkill).Learn(xp);
                }

                List<Thing> ingredients = CalculateIngredients(curJob, actor);
                Thing dominantIngredient = CalculateDominantIngredient(curJob, ingredients);
                ThingStyleDef style = null;
                if (ModsConfig.IdeologyActive && curJob.bill.recipe.products != null && curJob.bill.recipe.products.Count == 1)
                {
                    style = ((!curJob.bill.globalStyle) ? curJob.bill.style : Faction.OfPlayer.ideos.PrimaryIdeo.style.StyleForThingDef(curJob.bill.recipe.ProducedThingDef)?.styleDef);
                }

                List<Thing> list = ((curJob.bill is Bill_Mech bill) ? GenRecipe.FinalizeGestatedPawns(bill, actor, style).ToList() : GenRecipe.MakeRecipeProducts(curJob.RecipeDef, actor, ingredients, dominantIngredient, jobDriver_DoBill.BillGiver, curJob.bill.precept, style, curJob.bill.graphicIndexOverride).ToList());
                ConsumeIngredients(ingredients, curJob.RecipeDef, actor.Map);
                curJob.bill.Notify_IterationCompleted(actor, ingredients);
                RecordsUtility.Notify_BillDone(actor, list);
                if (curJob?.bill == null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!GenPlace.TryPlaceThing(list[i], actor.Position, actor.Map, ThingPlaceMode.Near))
                        {
                            Log.Error(string.Concat(actor, " could not drop recipe product ", list[i], " near ", actor.Position));
                        }
                    }
                }
                else
                {
                    Thing thing = curJob.GetTarget(TargetIndex.B).Thing;
                    if (curJob.bill.recipe.WorkAmountTotal(thing) >= 10000f && list.Count > 0)
                    {
                        TaleRecorder.RecordTale(TaleDefOf.CompletedLongCraftingProject, actor, list[0].GetInnerIfMinified().def);
                    }

                    if (list.Any())
                    {
                        Find.QuestManager.Notify_ThingsProduced(actor, list);
                    }

                    if (list.Count == 0)
                    {
                        actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    else if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor)
                    {
                        for (int j = 0; j < list.Count; j++)
                        {
                            if (!GenPlace.TryPlaceThing(list[j], actor.Position, actor.Map, ThingPlaceMode.Near))
                            {
                                Log.Error($"{actor} could not drop recipe product {list[j]} near {actor.Position}");
                            }
                        }

                        actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    else
                    {
                        if (list.Count > 1)
                        {
                            for (int k = 1; k < list.Count; k++)
                            {
                                if (!GenPlace.TryPlaceThing(list[k], actor.Position, actor.Map, ThingPlaceMode.Near))
                                {
                                    Log.Error($"{actor} could not drop recipe product {list[k]} near {actor.Position}");
                                }
                            }
                        }

                        IntVec3 foundCell = IntVec3.Invalid;
                        TargetInfo exitSpot = TargetInfo.Invalid;
                        TargetInfo enterSpot = TargetInfo.Invalid;
                        if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                        {
                            StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(list[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, out foundCell, true, out exitSpot, out  enterSpot, out _);
                        }
                        else if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                        {
                            StoreAcrossMapsUtility.TryFindBestBetterStoreCellForIn(list[0], actor, StoragePriority.Unstored, actor.Faction, curJob.bill.GetSlotGroup(), out foundCell, true, out exitSpot, out enterSpot);
                        }
                        else
                        {
                            Log.ErrorOnce("Unknown store mode", 9158246);
                        }

                        if (foundCell.IsValid)
                        {
                            int num = actor.carryTracker.MaxStackSpaceEver(list[0].def);
                            if (num < list[0].stackCount)
                            {
                                int count = list[0].stackCount - num;
                                Thing thing2 = list[0].SplitOff(count);
                                if (!GenPlace.TryPlaceThing(thing2, actor.Position, actor.Map, ThingPlaceMode.Near))
                                {
                                    Log.Error($"{actor} could not drop recipe extra product that pawn couldn't carry, {thing2} near {actor.Position}");
                                }
                            }

                            if (num == 0)
                            {
                                actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                            }
                            else
                            {
                                actor.carryTracker.TryStartCarry(list[0]);
                                actor.jobs.StartJob(HaulAIAcrossMapsUtility.HaulToCellStorageJob(actor, list[0], foundCell, fitInStoreCell: false, TargetInfo.Invalid, TargetInfo.Invalid, exitSpot, enterSpot), JobCondition.Succeeded, null, resumeCurJobAfterwards: false, cancelBusyStances: true, null, null, fromQueue: false, canReturnCurJobToPool: false, true);
                            }
                        }
                        else
                        {
                            if (!GenPlace.TryPlaceThing(list[0], actor.Position, actor.Map, ThingPlaceMode.Near))
                            {
                                Log.Error($"Bill doer could not drop product {list[0]} near {actor.Position}");
                            }

                            actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                        }
                    }
                }
            };
            return toil;
        }

        public static bool TryGetNextDestinationFromQueue(TargetIndex primaryIndex, TargetIndex destIndex, ThingDef stuff, Job job, Pawn actor, out Thing target)
        {
            Thing primaryTarget = job.GetTarget(primaryIndex).Thing;
            target = null;
            if (actor.carryTracker?.CarriedThing == null)
            {
                return false;
            }

            bool hasSpareItems = actor.carryTracker.CarriedThing.stackCount > 0;
            if (primaryTarget != null && primaryTarget.Spawned && primaryTarget is IHaulEnroute enroute)
            {
                int spaceRemainingWithEnroute = enroute.GetSpaceRemainingWithEnroute(stuff, actor);
                hasSpareItems = actor.carryTracker.CarriedThing.stackCount > spaceRemainingWithEnroute;
            }

            target = GenClosestOnVehicle.ClosestThing_Global_Reachable(actor.Position, actor.Map, from x in job.GetTargetQueue(destIndex)
                                                                                         select x.Thing, PathEndMode.Touch, TraverseParms.For(actor), 99999f, Validator, null);
            return target != null;
            bool Validator(Thing th)
            {
                if (!(th is IHaulEnroute enroute2))
                {
                    return false;
                }

                if (enroute2.GetSpaceRemainingWithEnroute(stuff, actor) <= 0)
                {
                    return false;
                }

                if (th != primaryTarget && !hasSpareItems)
                {
                    return false;
                }

                return true;
            }
        }
    }
}