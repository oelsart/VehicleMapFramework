using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class ToilsAcrossMaps
{
    public static Toil GotoVehicleEnterSpot(TargetInfo enterSpot)
    {
        var toil = ToilMaker.MakeToil();
        toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;

        CompZipline compZipline = null;
        enterSpot.Thing?.TryGetComp(out compZipline);

        var dest = IntVec3.Invalid;
        if (compZipline is { Pair: not null })
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

            toil.tickIntervalAction = _ =>
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
        toil.FailOn(() => !enterSpot.IsValid || enterSpot.Map.BaseMapOrCaravan != toil.actor.BaseMapOrCaravan);
        return toil;
    }

    private static Toil OpenDoor(TargetInfo target, out Building_Door door, out Building_VehicleRamp ramp)
    {
        door = target.Cell.GetDoor(target.Map);
        ramp = target.Cell.GetFirstThing<Building_VehicleRamp>(target.Map);
        var door2 = door;
        var ramp2 = ramp;
        if ((door2, ramp2) is not (null, null))
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
        var toil = ToilMaker.MakeToil();
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
            var rot = Rot4.FromAngleFlat(normalized.AngleFlat());

            if (toil.actor.IsOnVehicleMapOf(out var vehicle))
            {
                normalized = normalized.RotatedBy(-vehicle.FullAngle);
                rot.AsInt -= vehicle.Rotation.AsInt;
            }
            toil.actor.Rotation = rot;

            //ジップラインの先端から登る場合は遅くなるわな
            var distance = comp.IsZiplineEnd ? distancePerTick * 0.5f : distancePerTick;
            var distanceSquared = (drawPosB - toil.actor.DrawPos).MagnitudeHorizontalSquared();
            var moveDistance = distanceSquared < distance * distance ? Mathf.Sqrt(distanceSquared) : distance;

            driver.drawOffset = (normalized * moveDistance * (GenTicks.TicksGame - initTick)).WithYOffset(0.1f);
            if (vehicle is null)
            {
                driver.drawOffset = driver.drawOffset.YOffsetFull();
            }

            var rect = Rect.MinMaxRect(drawPosA.x, drawPosA.z, drawPosB.x, drawPosB.z);
            // 裏返りを修正
            if (rect.xMin > rect.xMax) (rect.xMin, rect.xMax) = (rect.xMax, rect.xMin);
            if (rect.yMin > rect.yMax) (rect.yMin, rect.yMax) = (rect.yMax, rect.yMin);
            if (distanceSquared < 0.2f || !rect.ExpandedBy(3f).Contains(toil.actor.DrawPos.ToVector2()))
            {
                driver.ReadyForNextToil();
            }
        };
        toil.FailOn(() => !comp.parent.Spawned || comp.Pair is null or { Spawned: false });
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        return toil;

        //なんかUnityのnormalizedって重いらしいよ
        static Vector3 NormalizeFlat(Vector3 vec)
        {
            var length = vec.MagnitudeHorizontal();
            return new Vector3(vec.x / length, 0f, vec.z / length);
        }
    }

    private const float distancePerTick = 0.075f;

    public static IEnumerable<Toil> GotoTargetMap(JobDriverAcrossMaps driver, TraverseSpots spots)
    {
        var exitSpot = spots.exitSpot;
        var enterSpot = spots.enterSpot;
        var pawn = driver.pawn;
        var afterEnterMap = Toils_General.Label();
        yield return Toils_Jump.JumpIf(afterEnterMap, () => driver.Consumed(spots));
        yield return Toils_General.Do(() => driver.ConsumeSpots(spots));
        if (exitSpot is { Cell.IsValid: true, Map: not null })
        {
            CompZipline compZipline = null;
            exitSpot.Thing?.TryGetComp(out compZipline);

            var ability = pawn.abilities?.AllAbilitiesForReading.FirstOrDefault(a => a is Ability_MapTraverse);

            //あれ？もうexitSpotから出た後じゃない？ジャンプしよ
            var afterExitMap = Toils_General.Label();
            yield return Toils_Jump.JumpIf(afterExitMap, () => pawn.Map != exitSpot.Map);
            
            // 初期化
            var rot = Rot4.North;
            var cell = IntVec3.Invalid;
            var vehiclePawn = pawn as VehiclePawn;
            var vehicleOffset = vehiclePawn?.HalfLength() ?? 0;
            yield return Toils_General.Do(() =>
            {
                var flag = exitSpot.Map.IsVehicleMapOf(out var vehicle);
                if (!flag &&
                    !enterSpot.Map.IsVehicleMapOf(out vehicle)) return;
                rot = exitSpot.Thing?.Rotation ?? (flag
                    ? exitSpot.Cell.DirectionToInsideMap(vehicle)
                    : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).Opposite);
                cell = exitSpot.Cell + (vehicleOffset * rot.FacingCell);
            });
            var jumpTarget = Toils_General.Label();
            yield return Toils_Jump.JumpIf(jumpTarget, () => vehiclePawn?.VehicleRect().Contains(cell) ?? false);
            
            //exitSpotの場所まで行く。vehicleの場合はvehicleの長さ分手前に目的地を指定
            var gotoToil = ToilMaker.MakeToil();
            gotoToil.initAction = () =>
            {
                if (pawn.Position == cell)
                {
                    driver.ReadyForNextToil();
                    return;
                }
                pawn.pather.StartPath(cell, PathEndMode.OnCell);
            };
            gotoToil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return gotoToil;
            yield return jumpTarget;

            //ドアがあれば開ける
            var openDoor = OpenDoor(exitSpot, out var door, out var ramp);
            if (openDoor != null)
            {
                yield return openDoor;
            }

            var pos = exitSpot.Cell;
            if (compZipline is not null)
            {
                yield return ZiplineAnimation(driver, compZipline);
                if (compZipline.Pair is not null)
                    pos = compZipline.Pair.Position;
            }
            else
            {
                // GrapplingHookアビリティがある場合
                var abilityToil = Toils_General.Do(() =>
                {
                    if (ability is { CanCast.Accepted: true } && enterSpot is { Cell.IsValid: true, Map: not null })
                    {
                        if (pawn.TargetMap != enterSpot.Map)
                            pawn.TargetMap = enterSpot.Map;
                        ability.verb.TryStartCastOn(enterSpot.Cell);
                        driver.JumpToToil(afterEnterMap);
                    }
                });
                abilityToil.handlingFacing = true;

                var dist = 1;
                yield return Toils_Jump.JumpIf(abilityToil, () =>
                {
                    pos = CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, out dist, vehiclePawn);
                    return !pos.WalkableBy(exitSpot.Map.GroundMap, pawn);
                });
                
                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var offset = Vector3.zero;
                var ticks = 60f;
                var initTick = 0;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                toil2.initAction += () =>
                {
                    var flag = exitSpot.Map.IsVehicleMapOf(out var vehicle);
                    if (!flag &&
                        !enterSpot.Map.IsVehicleMapOf(out vehicle)) return;
                    rot = exitSpot.Thing?.Rotation ?? (flag
                        ? exitSpot.Cell.DirectionToInsideMap(vehicle)
                        : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).Opposite);
                    cell = exitSpot.Cell + (vehicleOffset * rot.FacingCell);
                    
                    initTick = GenTicks.TicksGame;
                    var baseRot = new Rot4(rot.AsInt + vehicle.Rotation.AsInt);
                    ticks = pawn.TicksPerMoveCardinal * 4f;
                    if (!exitSpot.HasThing) ticks *= 2f;
                    ticks *= dist + vehicleOffset;
                    driver.ticksLeftThisToil = (int)ticks;
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.SetPositionDirect(cell);
                        vehiclePawn.FullRotation = rot.Opposite;
                    }
                    else
                    {
                        pawn.Rotation = pawn.Drafted || pawn.HostileTo(Faction.OfPlayer)
                            ? baseRot.Opposite : rot.Opposite; // Thing.Rotationへのパッチとの兼ね合い
                    }
                    var initPos = GenThing.TrueCenter(pos, baseRot.Opposite, driver.pawn.def.size, 0f);
                    if (pawn.def.size.x % 2 == 0 &&
                        ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                         (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                         vehicle.Rotation == Rot4.South))
                    {
                        initPos += baseRot.IsHorizontal ? Vector3.back : Vector3.right;
                    }
                    var initPos2 = GenThing.TrueCenter(cell, rot.Opposite, pawn.def.size, 0f).ToBaseMapCoord(vehicle).Yto0();
                    offset = initPos - initPos2;
                };

                toil2.tickAction = () =>
                {
                    if (!exitSpot.Map.IsVehicleMapOf(out var vehicle)) return;
                    driver.drawOffset = offset.RotatedBy(-vehicle.FullRotation.AsAngle) * ((GenTicks.TicksGame - initTick) / ticks);
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                };

                yield return toil2.FailOn(() => exitSpot.Map?.Disposed ?? true);
                var mapCheck = Toils_Jump.JumpIf(afterExitMap, () => pawn.MapHeld != exitSpot.Map);
                yield return Toils_Jump.Jump(mapCheck);
                yield return abilityToil;
                yield return mapCheck;
            }
            //デスポーン後目的地のマップにリスポーン。スポーン地の再計算時にそこが埋まってたらとりあえず失敗に
            var toil3 = ToilMaker.MakeToil();
            toil3.defaultCompleteMode = ToilCompleteMode.Instant;
            toil3.initAction = () =>
            {
                if (!exitSpot.Map.IsVehicleMapOf(out var vehicle)) return;
                Map map;
                if (compZipline != null)
                {
                    map = compZipline.Pair.Map;
                    rot = toil3.actor.Rotation;
                }
                else
                {
                    map = exitSpot.Map.BaseMap();
                    rot = exitSpot.HasThing ? exitSpot.Thing.BaseFullRotation() : exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    rot = rot.Opposite;
                }

                driver.drawOffset = Vector3.zero;
                if (vehiclePawn != null)
                {
                    vehiclePawn.DeSpawnWithoutJobClearVehicle();
                    GenSpawn.Spawn(toil3.actor, pos, map, rot);
                }
                else
                {
                    toil3.actor.DeSpawnWithoutJobClear();
                    if (toil3.actor.roping != null)
                    {
                        foreach (var ropee in toil3.actor.roping.Ropees)
                        {
                            ropee.DeSpawnWithoutJobClear();
                        }
                    }
                    GenSpawn.Spawn(toil3.actor, pos, map, rot);
                    if (toil3.actor.roping != null)
                    {
                        foreach (var ropee in toil3.actor.roping.Ropees)
                        {
                            GenSpawn.Spawn(ropee, pos, map, rot);
                        }
                    }
                }
            };
            yield return toil3.FailOn(() => exitSpot is { Cell.IsValid: true, Map: not { Disposed: false }});
            yield return afterExitMap;
        }
        if (enterSpot is { Cell.IsValid: true, Map: not null })
        {
            CompZipline compZipline = null;
            enterSpot.Thing?.TryGetComp(out compZipline);

            //あれ？もうenterSpotのマップに居ない？ジャンプしよ
            yield return Toils_Jump.JumpIf(afterEnterMap, () => pawn.MapHeld != enterSpot.Map.GroundMap || pawn.MapHeld == enterSpot.Map);

            //enterSpotの手前の場所まで行く。vehicleの長さ分のオフセットはメソッド内でやっている
            var vehiclePawn = pawn as VehiclePawn;
            var toil = GotoVehicleEnterSpot(enterSpot);
            yield return toil;

            //ドアがあれば開ける
            var openDoor = OpenDoor(enterSpot, out var door, out var ramp);
            if (openDoor != null)
            {
                yield return openDoor;
            }

            VehiclePawnWithMap vehicle = null;
            CompZipline pairComp;
            if (compZipline != null && (pairComp = compZipline.Pair?.TryGetComp<CompZipline>()) != null)
            {
                yield return ZiplineAnimation(driver, pairComp);
            }
            else
            {
                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = pawn.TicksPerMoveCardinal * 4f;
                if (!enterSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                var initPos3 = Vector3.zero;
                var offset = Vector3.zero;
                var initTick = 0;
                toil2.initAction += () =>
                {
                    enterSpot.Map.IsVehicleMapOf(out vehicle);
                    var baseRot8 = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotation() : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle);
                    Rot4 baseRot4 = baseRot8;
                    var cell = CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, out var dist, vehiclePawn);
                    var vehicleOffset = vehiclePawn?.HalfLength() ?? 0;
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
                    var initPos = GenThing.TrueCenter(enterSpot.Cell + (faceCell2 * vehicleOffset), rot, pawn.def.size, 0f).ToBaseMapCoord(vehicle).Yto0();
                    if (pawn.def.size.x % 2 == 0 &&
                        ((vehicle.Rotation == Rot4.East && rot.IsHorizontal) ||
                         (vehicle.Rotation == Rot4.West && !rot.IsHorizontal) ||
                         vehicle.Rotation == Rot4.South)
                    )
                    {
                        initPos += baseRot4.IsHorizontal ? Vector3.forward : Vector3.left;
                    }
                    var initPos2 = GenThing.TrueCenter(cell, baseRot8, pawn.def.size, 0f);
                    initPos3 = enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().Yto0();
                    offset = initPos - initPos2;
                    initTick = GenTicks.TicksGame;
                };

                toil2.tickAction = () =>
                {
                    driver.drawOffset = (offset * ((GenTicks.TicksGame - initTick) / ticks)) + enterSpot.Cell.ToBaseMapCoord(vehicle).ToVector3().WithY(Altitudes.AltInc * 50f) - initPos3;
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                };
                yield return toil2.FailOn(() => enterSpot.Map?.Disposed ?? true);
            }

            var toil3 = ToilMaker.MakeToil();
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
                    GenSpawn.Spawn(toil3.actor, enterSpot.Cell + (rot.FacingCell * vehiclePawn.HalfLength()), enterSpot.Map, rot);
                }
                else
                {
                    toil3.actor.DeSpawnWithoutJobClear();
                    if (toil3.actor.roping != null)
                    {
                        foreach (var ropee in toil3.actor.roping.Ropees)
                        {
                            ropee.DeSpawnWithoutJobClear();
                        }
                    }
                    GenSpawn.Spawn(toil3.actor, enterSpot.Cell, enterSpot.Map, rot, WipeMode.VanishOrMoveAside);
                    if (toil3.actor.roping != null)
                    {
                        foreach (var ropee in toil3.actor.roping.Ropees)
                        {
                            GenSpawn.Spawn(ropee, enterSpot.Cell, enterSpot.Map, rot, WipeMode.VanishOrMoveAside);
                        }
                    }
                }
            };
            yield return toil3.FailOn(() => enterSpot is { Cell.IsValid: true, Map: not { Disposed: false }});
        }
        yield return afterEnterMap;
    }
}