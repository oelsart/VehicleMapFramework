using System;
using System.Collections.Generic;
using RimWorld;
using SmashTools;
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

    private static Toil OpenDoor(JobDriver driver, TargetInfo target)
    {
        var waitOpen = Toils_General.Wait(0);
        waitOpen.handlingFacing = true;
        waitOpen.initAction += () =>
        {
            var door = target.Cell.GetDoor(target.Map);
            var ramp = target.Cell.GetFirstThing<Building_VehicleRamp>(target.Map);
            waitOpen.defaultDuration = Math.Max(door?.TicksToOpenNow ?? 0, ramp?.TicksToOpenNow ?? 0);
            driver.ticksLeftThisToil = waitOpen.defaultDuration;
            door?.StartManualOpenBy(waitOpen.actor);
            ramp?.StartManualOpenBy(waitOpen.actor);
        };
        return waitOpen;
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
        var afterConsumeSpots = Toils_General.Label();
        yield return Toils_Jump.JumpIf(afterConsumeSpots, () => driver.Consumed(spots));
        if (exitSpot.IsValid)
        {
            CompZipline compZipline = null;
            exitSpot.Thing?.TryGetComp(out compZipline);

            var ability = pawn.abilities?.AllAbilitiesForReading.FirstOrDefault(a => a is Ability_MapTraverse);

            //あれ？もうexitSpotから出た後じゃない？ジャンプしよ
            var afterExitMap = Toils_General.Label();
            yield return Toils_Jump.JumpIf(afterExitMap, () => exitSpot.Map is null || pawn.Map != exitSpot.Map);

            var vehiclePawn = pawn as VehiclePawn;
            var vehicleOffset = vehiclePawn?.HalfLength() ?? 0;
            
            {
                var cell = IntVec3.Invalid;
                yield return Toils_General.Do(() =>
                {
                    if (!exitSpot.Map.IsVehicleMapOf(out var vehicle)) return;
                    var rot = exitSpot.Cell.DirectionToInsideMap(vehicle);
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
            }

            //ドアがあれば開ける
            yield return OpenDoor(driver, exitSpot);

            if (compZipline is not null)
            {
                yield return ZiplineAnimation(driver, compZipline);
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

                yield return Toils_Jump.JumpIf(abilityToil, () =>
                {
                    var cell = CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, vehiclePawn);
                    return !cell.WalkableBy(exitSpot.Map.GroundMap, pawn);
                });
                
                //マップ移動アニメーション。目的地の計算の後tick毎の描画位置を計算。ドアは開け続けておく
                var ticks = pawn.TicksPerMoveCardinal * 4f;
                if (!exitSpot.HasThing) ticks *= 2f;
                var toil2 = Toils_General.Wait((int)ticks);
                toil2.handlingFacing = true;
                toil2.initAction += () =>
                {
                    CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, out var dist, vehiclePawn);
                    ticks *= Mathf.Max(dist + vehicleOffset, 1);
                    toil2.defaultDuration = (int)ticks;
                    driver.ticksLeftThisToil = toil2.defaultDuration;
                };

                toil2.tickAction = () =>
                {
                    if (!exitSpot.Map.IsVehicleMapOf(out var vehicle)) return;
                    CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, out var dist, vehiclePawn);
                    dist += vehicleOffset;
                    var initPos = exitSpot.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle);
                    var rot = exitSpot.Cell.DirectionToInsideMap(vehicle);
                    var baseRot = exitSpot.Cell.BaseFullDirectionToInsideMap(vehicle);

                    var offset = ((initPos + baseRot.Opposite.AsVector2.ToVector3() * dist) - initPos).Yto0();
                    var totalTick = toil2.defaultDuration;
                    driver.drawOffset = offset * ((totalTick - driver.ticksLeftThisToil) / (float)totalTick);
                    
                    var door = exitSpot.Cell.GetDoor(exitSpot.Map);
                    var ramp = exitSpot.Cell.GetFirstThing<Building_VehicleRamp>(exitSpot.Map);
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);
                    
                    if (vehiclePawn != null)
                    {
                        vehiclePawn.FullRotation = rot.Opposite;
                    }
                    else
                    {
                        if (pawn.Drafted || pawn.HostileTo(Faction.OfPlayer))
                        {
                            rot.AsInt += vehicle.Rotation.AsInt; // Thing.Rotationへのパッチとの兼ね合い
                        }
                        pawn.Rotation = rot.Opposite;
                    }
                };

                yield return toil2.FailOn(() => exitSpot is { Cell.IsValid: true, Map: not { Disposed: false } });
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
                var cell = CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, vehiclePawn);
                if (!cell.IsValid) return;
                Rot4 rot;
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
                    GenSpawn.Spawn(toil3.actor, cell, map, rot);
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
                    GenSpawn.Spawn(toil3.actor, cell, map, rot);
                    if (toil3.actor.roping != null)
                    {
                        foreach (var ropee in toil3.actor.roping.Ropees)
                        {
                            GenSpawn.Spawn(ropee, cell, map, rot);
                        }
                    }
                }
            };
            yield return toil3.FailOn(() => exitSpot is { Cell.IsValid: true, Map: not { Disposed: false }});
            yield return afterExitMap;
        }
        if (enterSpot.IsValid)
        {
            CompZipline compZipline = null;
            enterSpot.Thing?.TryGetComp(out compZipline);

            //あれ？もうenterSpotのマップに居ない？ジャンプしよ
            yield return Toils_Jump.JumpIf(afterEnterMap, () => enterSpot.Map is null || pawn.MapHeld != enterSpot.Map.GroundMap || pawn.MapHeld == enterSpot.Map);

            //enterSpotの手前の場所まで行く。vehicleの長さ分のオフセットはメソッド内でやっている
            var vehiclePawn = pawn as VehiclePawn;
            var toil = GotoVehicleEnterSpot(enterSpot);
            yield return toil;

            //ドアがあれば開ける
            yield return OpenDoor(driver, enterSpot);

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
                toil2.initAction += () =>
                {
                    CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, out var dist, vehiclePawn);
                    ticks *= Mathf.Max(dist + (vehiclePawn?.HalfLength() ?? 0), 1);
                    toil2.defaultDuration = (int)ticks;
                    driver.ticksLeftThisToil = toil2.defaultDuration;
                };

                toil2.tickAction = () =>
                {
                    if (!enterSpot.Map.IsVehicleMapOf(out var vehicle)) return;
                    var cell = CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, vehiclePawn);
                    if (!cell.IsValid) return;

                    var offset = (enterSpot.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle) - cell.ToVector3Shifted()).Yto0();
                    var totalTick = toil2.defaultDuration;
                    driver.drawOffset = offset * ((totalTick - driver.ticksLeftThisToil) / (float)totalTick);
                    if (vehiclePawn is not null)
                        driver.drawOffset.y = Altitudes.AltInc * 30f;
                    
                    var door = enterSpot.Cell.GetDoor(enterSpot.Map);
                    var ramp = enterSpot.Cell.GetFirstThing<Building_VehicleRamp>(enterSpot.Map);
                    door?.StartManualOpenBy(toil2.actor);
                    ramp?.StartManualOpenBy(toil2.actor);

                    if (vehiclePawn != null)
                    {
                        vehiclePawn.FullRotation = Rot8.FromAngle(offset.AngleFlat());
                    }
                    else
                    {
                        pawn.Rotation = Rot4.FromAngleFlat(offset.AngleFlat());
                    }
                };
                yield return toil2.FailOn(() => enterSpot is { Cell.IsValid: true, Map: not { Disposed: false }});
            }

            var toil3 = ToilMaker.MakeToil();
            toil3.defaultCompleteMode = ToilCompleteMode.Instant;
            toil3.initAction = () =>
            {
                if (!enterSpot.Map.IsVehicleMapOf(out var vehicle)) return;
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
        yield return Toils_General.Do(() => driver.ConsumeSpots(spots));
        yield return afterConsumeSpots;
    }
}