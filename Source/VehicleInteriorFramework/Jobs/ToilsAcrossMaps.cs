using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors;

public static class ToilsAcrossMaps
{
    public static Toil GotoVehicleEnterSpot(TargetInfo enterSpot)
    {
        Toil toil = ToilMaker.MakeToil("GotoThingOnVehicle");
        toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;

        CompZipline compZipline = null;
        enterSpot.Thing?.TryGetComp(out compZipline);

        IntVec3 dest = IntVec3.Invalid;
        var baseMap = enterSpot.Map.BaseMap();
        if (compZipline != null && compZipline.Pair != null)
        {
            dest = compZipline.Pair.Position;
            toil.FailOn(() =>
            {
                var result = !compZipline.Pair?.Spawned ?? true;
                if (result) toil.actor.Drawer.tweener.ResetTweenedPosToRoot();
                return result;
            });
        }
        else
        {
            toil.initAction = delegate ()
            {
                dest = ReachabilityUtilityOnVehicle.EnterVehiclePosition(enterSpot, toil.actor as VehiclePawn);
            };

            toil.tickAction = () =>
            {
                var curDest = ReachabilityUtilityOnVehicle.EnterVehiclePosition(enterSpot, toil.actor as VehiclePawn);
                if (dest != curDest)
                {
                    dest = curDest;
                    toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
                }
            };
        }
        toil.initAction += () =>
        {
            if (toil.actor.Position == dest)
            {
                toil.actor.jobs.curDriver.ReadyForNextToil();
                return;
            }
            toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
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

    private static Toil OpenDoor(TargetInfo target, out Building_Door door, out Building_VehicleRamp ramp)
    {
        door = target.Cell.GetDoor(target.Map);
        target.Cell.TryGetFirstThing(target.Map, out ramp);
        var door2 = door;
        var ramp2 = ramp;
        if (door2 != null || ramp2 != null)
        {
            var waitOpen = Toils_General.Wait(Math.Max(door2?.TicksToOpenNow ?? 0, ramp2?.TicksToOpenNow ?? 0));
            waitOpen.initAction += () =>
            {
                door2?.StartManualOpenBy(waitOpen.actor);
                ramp2?.StartManualOpenBy(waitOpen.actor);
            };
            return waitOpen;
        }
        return null;
    }

    private static Toil ZiplineAnimation(JobDriverAcrossMaps driver, CompZipline comp)
    {
        //なんかUnityのnormalizedって重いらしいよ
        static Vector3 NormalizeFlat(Vector3 vec)
        {
            var length = vec.MagnitudeHorizontal();
            return new Vector3(vec.x / length, 0f, vec.z / length);
        }

        Toil toil = ToilMaker.MakeToil("ZiplineAnimation");
        toil.handlingFacing = true;
        var initTick = 0;
        toil.initAction = () =>
        {
            toil.actor.pather.StopDead();
            initTick = GenTicks.TicksGame;
        };
        toil.tickAction = () =>
        {
            var drawPosA = comp.parent.DrawPos;
            var drawPosB = comp.Pair.DrawPos;
            var normalized = NormalizeFlat(drawPosB - drawPosA);
            toil.actor.Rotation = Rot4.FromAngleFlat(normalized.AngleFlat());

            if (toil.actor.IsOnVehicleMapOf(out var vehicle))
            {
                normalized = normalized.RotatedBy(-vehicle.FullRotation.AsAngle);
            }

            //ジップラインの先端から登る場合は遅くなるわな
            var distance = comp.IsZiplineEnd ? distancePerTick * 0.5f : distancePerTick;
            var distanceSquared = (drawPosB - toil.actor.DrawPos).MagnitudeHorizontalSquared();
            var moveDistance = distanceSquared < distance * distance ? Mathf.Sqrt(distanceSquared) : distance;

            driver.drawOffset = normalized * moveDistance * (GenTicks.TicksGame - initTick);
            if (vehicle == null)
            {
                driver.drawOffset.y += VehicleMapUtility.altitudeOffsetFull;
            }

            if (distanceSquared < 0.2f)
            {
                driver.ReadyForNextToil();
            }
        };
        toil.FailOn(() =>
        {
            return !comp.parent.Spawned || (!comp.Pair?.Spawned ?? true);
        });
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        return toil;
    }

    private const float distancePerTick = 0.075f;

    public static IEnumerable<Toil> GotoTargetMap(JobDriverAcrossMaps driver, TargetInfo exitSpot, TargetInfo enterSpot)
    {
        if (exitSpot.IsValid && exitSpot.Map != null)
        {
            CompZipline compZipline = null;
            exitSpot.Thing?.TryGetComp(out compZipline);

            //あれ？もうexitSpotから出た後じゃない？ジャンプしよ
            var afterExitMap = Toils_General.Label();
            yield return Toils_Jump.JumpIf(afterExitMap, () =>
            {
                return driver.pawn.Map != exitSpot.Map;
            });

            exitSpot.Map.IsVehicleMapOf(out var vehicle);
            //exitSpotの場所まで行く。vehicleの場合はvehicleの長さ分手前に目的地を指定
            var vehiclePawn = driver.pawn as VehiclePawn;
            var rot = exitSpot.HasThing ? exitSpot.Thing.Rotation : exitSpot.Cell.DirectionToInsideMap(vehicle);
            var vehicleOffset = vehiclePawn != null ? vehiclePawn.HalfLength() : 0;
            var cell = exitSpot.Cell + (vehicleOffset * rot.FacingCell);
            var toil = Toils_Goto.GotoCell(cell, PathEndMode.OnCell);
            yield return toil;

            //ドアがあれば開ける
            var openDoor = OpenDoor(exitSpot, out var door, out var ramp);
            if (openDoor != null)
            {
                yield return openDoor;
            }

            IntVec3 pos = exitSpot.Cell;
            if (compZipline != null)
            {
                yield return ZiplineAnimation(driver, compZipline);
                pos = compZipline.Pair.Position;
            }
            else
            {
                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = driver.pawn.TicksPerMoveCardinal * 4f;
                if (!exitSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var offset = Vector3.zero;
                var initTick = 0;
                toil2.initAction += () =>
                {
                    var baseMap = exitSpot.Map.BaseMap();
                    initTick = GenTicks.TicksGame;
                    Rot4 baseRot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation() : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    pos = ReachabilityUtilityOnVehicle.EnterVehiclePosition(exitSpot, out var dist, vehiclePawn);
                    ticks *= dist + vehicleOffset;
                    driver.ticksLeftThisToil = (int)ticks;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.SetPositionDirect(cell);
                        vehiclePawn.FullRotation = rot.Opposite;
                    }
                    else
                    {
                        toil2.actor.Rotation = baseRot.Opposite;
                    }
                    var initPos = GenThing.TrueCenter(pos, baseRot.Opposite, driver.pawn.def.size, 0f);
                    if (driver.pawn.def.size.x % 2 == 0 &&
                    ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                    (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                    vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot.IsHorizontal ? Vector3.back : Vector3.right;
                    }
                    var initPos2 = GenThing.TrueCenter(cell, rot.Opposite, driver.pawn.def.size, 0f).ToBaseMapCoord(vehicle).WithY(0f);
                    offset = initPos - initPos2;
                };

                toil2.tickAction = () =>
                {
                    driver.drawOffset = offset.RotatedBy(-vehicle.FullRotation.AsAngle) * ((GenTicks.TicksGame - initTick) / ticks);
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                };

                //toil2.FailOn(() => !exitSpot.Cell.Standable(exitSpot.Map));
                yield return toil2.FailOn(() => exitSpot.Map?.Disposed ?? true);
            }

            //デスポーン後目的地のマップにリスポーン。スポーン地の再計算時にそこが埋まってたらとりあえず失敗に
            var toil3 = ToilMaker.MakeToil("Exit Vehicle Map");
            toil3.defaultCompleteMode = ToilCompleteMode.Instant;
            toil3.initAction = () =>
            {
                Map map;
                if (compZipline != null)
                {
                    map = compZipline.Pair.Map;
                    rot = toil3.actor.Rotation;
                }
                else
                {
                    map = exitSpot.Map.BaseMap();
                    var basePos = exitSpot.HasThing ? exitSpot.Thing.PositionOnBaseMap() : exitSpot.Cell.ToBaseMapCoord(vehicle);
                    rot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation() : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    rot = rot.Opposite;
                }

                driver.drawOffset = Vector3.zero;
                if (vehiclePawn != null)
                {
                    vehiclePawn.DeSpawnWithoutJobClearVehicle();
                    GenSpawn.Spawn(toil3.actor, pos, map, rot, WipeMode.Vanish);
                }
                else
                {
                    toil3.actor.DeSpawnWithoutJobClear();
                    foreach (var ropee in toil3.actor.roping?.Ropees)
                    {
                        ropee.DeSpawnWithoutJobClear();
                    }
                    GenSpawn.Spawn(toil3.actor, pos, map, rot, WipeMode.Vanish);
                    foreach (var ropee in toil3.actor.roping?.Ropees)
                    {
                        GenSpawn.Spawn(ropee, pos, map, rot, WipeMode.Vanish);
                    }
                }
            };
            yield return toil3.FailOn(() => (exitSpot.Map?.Disposed ?? true) || exitSpot.Map.BaseMap() == exitSpot.Map);
            yield return afterExitMap;
        }
        if (enterSpot.IsValid && enterSpot.Map != null)
        {
            CompZipline compZipline = null;
            enterSpot.Thing?.TryGetComp(out compZipline);

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
            var openDoor = OpenDoor(enterSpot, out var door, out var ramp);
            if (openDoor != null)
            {
                yield return openDoor;
            }

            enterSpot.Map.IsVehicleMapOf(out var vehicle);
            CompZipline pairComp;
            if (compZipline != null && (pairComp = compZipline.Pair?.TryGetComp<CompZipline>()) != null)
            {
                yield return ZiplineAnimation(driver, pairComp);
            }
            else
            {
                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = driver.pawn.TicksPerMoveCardinal * 4f;
                if (!enterSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var initPos3 = Vector3.zero;
                var offset = Vector3.zero;
                var initTick = 0;
                toil2.initAction += () =>
                {
                    var baseMap = enterSpot.Map.BaseMap();
                    var baseRot8 = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotation() : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    Rot4 baseRot4 = baseRot8;
                    var cell = ReachabilityUtilityOnVehicle.EnterVehiclePosition(enterSpot, out var dist, vehiclePawn);
                    var vehicleOffset = vehiclePawn != null ? vehiclePawn.HalfLength() : 0;
                    ticks *= dist + vehicleOffset;
                    driver.ticksLeftThisToil = (int)ticks;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.SetPositionDirect(cell);
                        vehiclePawn.FullRotation = baseRot8;
                    }
                    else
                    {
                        toil2.actor.Rotation = baseRot4;
                    }
                    var rot = enterSpot.HasThing ? enterSpot.Thing.Rotation : enterSpot.Cell.DirectionToInsideMap(vehicle);
                    var faceCell2 = rot.FacingCell;
                    var initPos = GenThing.TrueCenter(enterSpot.Cell + (faceCell2 * vehicleOffset), rot, driver.pawn.def.size, 0f).ToBaseMapCoord(vehicle).WithY(0f);
                    if (driver.pawn.def.size.x % 2 == 0 &&
                    ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                    (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                    vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot4.IsHorizontal ? Vector3.forward : Vector3.left;
                    }
                    var initPos2 = GenThing.TrueCenter(cell, baseRot8, driver.pawn.def.size, 0f);
                    initPos3 = enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().WithY(0f);
                    offset = initPos - initPos2;
                    initTick = GenTicks.TicksGame;
                };

                toil2.tickAction = () =>
                {
                    driver.drawOffset = (offset * ((GenTicks.TicksGame - initTick) / ticks)) + enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().WithY(VehicleMapUtility.altitudeOffsetFull) - initPos3;
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                };
                yield return toil2.FailOn(() => enterSpot.Map?.Disposed ?? true);
            }

            var toil3 = ToilMaker.MakeToil("Enter Vehicle Map");
            toil3.defaultCompleteMode = ToilCompleteMode.Instant;
            toil3.initAction = () =>
            {
                driver.drawOffset = Vector3.zero;
                Rot4 rot;
                if (compZipline != null)
                {
                    rot = toil3.actor.Rotation;
                }
                else
                {
                    rot = enterSpot.HasThing ? enterSpot.Thing.Rotation : enterSpot.Cell.DirectionToInsideMap(vehicle);
                }

                if (vehiclePawn != null)
                {
                    vehiclePawn.DeSpawnWithoutJobClearVehicle();
                    GenSpawn.Spawn(toil3.actor, enterSpot.Cell + (rot.FacingCell * vehiclePawn.HalfLength()), enterSpot.Map, rot, WipeMode.Vanish);
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
            yield return toil3.FailOn(() => enterSpot.Map?.Disposed ?? true);
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

                        if (curJob.def == JobDefOf.DoBill)
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

    private static readonly List<IntVec3> yieldedIngPlaceCells = [];

    private static IEnumerable<IntVec3> IngredientPlaceCellsInOrder(Thing destination)
    {
        yieldedIngPlaceCells.Clear();
        try
        {
            IntVec3 interactCell = destination.Position;
            if (destination is IBillGiver billGiver)
            {
                interactCell = ((Thing)billGiver).InteractionCell;
                foreach (IntVec3 item in billGiver.IngredientStackCells.OrderBy(c => (c - interactCell).LengthHorizontalSquared))
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
            if (th is not IHaulEnroute enroute2)
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