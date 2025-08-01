using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class ToilsAcrossMaps
{
    public static Toil GotoVehicleEnterSpot(TargetInfo enterSpot)
    {
        Toil toil = ToilMaker.MakeToil("GotoVehicleEnterSpot");
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
            toil.initAction = () =>
            {
                dest = CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, toil.actor as VehiclePawn);
            };

            toil.tickAction = () =>
            {
                var curDest = CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, toil.actor as VehiclePawn);
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
                driver.drawOffset = driver.drawOffset.YOffsetFull();
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
                    pos = CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, out var dist, vehiclePawn);
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
                    var initPos2 = GenThing.TrueCenter(cell, rot.Opposite, driver.pawn.def.size, 0f).ToBaseMapCoord(vehicle).Yto0();
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
            var toil = GotoVehicleEnterSpot(enterSpot);
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
                    var cell = CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, out var dist, vehiclePawn);
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
                    var initPos = GenThing.TrueCenter(enterSpot.Cell + (faceCell2 * vehicleOffset), rot, driver.pawn.def.size, 0f).ToBaseMapCoord(vehicle).Yto0();
                    if (driver.pawn.def.size.x % 2 == 0 &&
                    ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                    (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                    vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot4.IsHorizontal ? Vector3.forward : Vector3.left;
                    }
                    var initPos2 = GenThing.TrueCenter(cell, baseRot8, driver.pawn.def.size, 0f);
                    initPos3 = enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().Yto0();
                    offset = initPos - initPos2;
                    initTick = GenTicks.TicksGame;
                };

                toil2.tickAction = () =>
                {
                    var drawPos = driver.pawn.DrawPos;
                    driver.drawOffset = (offset * ((GenTicks.TicksGame - initTick) / ticks)) + enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().WithY(drawPos.YOffsetFull(vehicle).y - drawPos.y) - initPos3;
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
}