using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class ToilsAcrossMaps
{
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
      var comp = exitSpot.Thing?.TryGetComp<CompVehicleEnterSpot>();

      var ability = pawn.abilities?.AllAbilitiesForReading.FirstOrDefault(a => a is Ability_MapTraverse);

      //あれ？もうexitSpotから出た後じゃない？ジャンプしよ
      var afterExitMap = Toils_General.Label();
      yield return Toils_Jump.JumpIf(afterExitMap, () => exitSpot.Map is null || pawn.Map != exitSpot.Map);

      var vehiclePawn = pawn as VehiclePawn;

      static IntVec3 ExitCell(Pawn pawn, TargetInfo exitSpot)
      {
        if (!exitSpot.Map.IsVehicleMapOf(out var vehicle)) return IntVec3.Invalid;
        var cell = exitSpot.Cell;
        if (pawn is VehiclePawn vehiclePawn)
        {
          var rot = exitSpot.Cell.DirectionToInsideMap(vehicle);
          cell += vehiclePawn.HalfLength() * rot.FacingCell;
        }
        return cell;
      }

      var jumpTarget = Toils_General.Label();
      yield return Toils_Jump.JumpIf(jumpTarget, () => vehiclePawn?.VehicleRect(true).Contains(ExitCell(pawn, exitSpot)) ?? false);

      //exitSpotの場所まで行く。vehicleの場合はvehicleの長さ分手前に目的地を指定
      var gotoToil = ToilMaker.MakeToil();
      gotoToil.initAction = () =>
      {
        var cell = ExitCell(pawn, exitSpot);
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
      yield return OpenDoor(driver, exitSpot);

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
        if (comp is not null) return false;
        var pos = CrossMapReachabilityUtility.EnterVehiclePosition(exitSpot, vehiclePawn);
        var groundMap = exitSpot.Map.GroundMap;
        return !pos.IsValid || !pos.WalkableBy(groundMap, pawn) || pos.GetTerrain(groundMap) is { dangerous: true };
      });
      yield return TraverseAnimationToil(driver, exitSpot, comp);
      
      var mapCheck = Toils_Jump.JumpIf(afterExitMap, () => pawn.MapHeld != exitSpot.Map);
      yield return Toils_Jump.Jump(mapCheck);
      yield return abilityToil;
      yield return mapCheck;

      yield return TraverseToil(exitSpot, comp);
      yield return afterExitMap;
    }

    if (enterSpot.IsValid)
    {
      var comp = enterSpot.Thing?.TryGetComp<CompVehicleEnterSpot>();

      //あれ？もうenterSpotのマップに居ない？ジャンプしよ
      yield return Toils_Jump.JumpIf(afterEnterMap,
        () => enterSpot.Map is null || pawn.MapHeld != enterSpot.Map.GroundMap || pawn.MapHeld == enterSpot.Map);

      //enterSpotの手前の場所まで行く。vehicleの長さ分のオフセットはメソッド内でやっている
      yield return GotoVehicleEnterSpot(enterSpot);

      //ドアがあれば開ける
      yield return OpenDoor(driver, enterSpot);
      yield return TraverseAnimationToil(driver, enterSpot, comp);
      yield return TraverseToil(enterSpot, comp);
    }

    yield return afterEnterMap;
    yield return Toils_General.Do(() => driver.ConsumeSpots(spots));
    yield return afterConsumeSpots;
  }
  
  private static Toil GotoVehicleEnterSpot(TargetInfo enterSpot)
  {
    var toil = ToilMaker.MakeToil();
    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;

    var comp = enterSpot.Thing?.TryGetComp<CompVehicleEnterSpot>();
    var dest = default(LocalTargetInfo); 
    toil.initAction = () =>
    {
      dest = GetDest(comp, enterSpot, toil.actor);
      if (toil.actor.Position == dest)
      {
        toil.actor.jobs.curDriver.ReadyForNextToil();
        return;
      }
      toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
    };

    toil.tickIntervalAction = _ =>
    {
      var curDest = GetDest(comp, enterSpot, toil.actor);
      if (dest != curDest)
      {
        dest = curDest;
        toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
      }
    };

    toil.FailOn(() => !dest.IsValid);
    toil.FailOn(() => !enterSpot.IsValid || enterSpot.Map.BaseMapOrCaravan != toil.actor.BaseMapOrCaravan);
    return toil;
    
    static LocalTargetInfo GetDest(CompVehicleEnterSpot comp, TargetInfo spot, Pawn actor) =>
      comp?.AvailableAccessSpot.Cell ?? CrossMapReachabilityUtility.EnterVehiclePosition(spot, actor as VehiclePawn);
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

  private static Toil TraverseAnimationToil(JobDriverAcrossMaps driver, TargetInfo enterSpot, [CanBeNull] CompVehicleEnterSpot comp)
  {
    var toil = ToilMaker.MakeToil();
    toil.handlingFacing = true;
    var initTick = 0;
    toil.initAction = () =>
    {
      initTick = GenTicks.TicksGame;
    };
    toil.tickAction = () =>
    {
      var startOffset = Vector3.zero;
      var drawPosA = enterSpot.CenterVector3OnGroundMap;
      var drawPosB = comp is not null
        ? comp.AvailableAccessSpot.CenterVector3OnGroundMap
        : CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot).ToVector3Shifted();
      var drawPosC = toil.actor.DrawPos;
      
      if (enterSpot.Map != toil.actor.Map)
      {
        (drawPosA, drawPosB) = (drawPosB, drawPosA);
        driver.drawOffset = Vector3.zero;
        startOffset = (drawPosA - toil.actor.DrawPos).Yto0();
      }
      var normalized = NormalizeFlat(drawPosB - drawPosA);
      var rot = Pawn_RotationTracker.RotFromAngleBiased(normalized.AngleFlat());

      if (toil.actor.IsOnNonFocusedVehicleMapOf(out var vehicle))
      {
        normalized = normalized.RotatedBy(-vehicle.FullAngle);
        if (!toil.actor.Drafted) rot.AsInt -= vehicle.Rotation.AsInt;
      }

      toil.actor.Rotation = rot;

      var distance = comp?.MovePerTick(toil.actor) ?? (0.15f / toil.actor.TicksPerMoveCardinal);
      var distanceSquared = (drawPosB - drawPosC).MagnitudeHorizontalSquared();
      var moveDistance = distanceSquared < distance * distance ? Mathf.Sqrt(distanceSquared) : distance;

      driver.drawOffset = startOffset + normalized * moveDistance * (GenTicks.TicksGame - initTick);
      driver.drawOffset.y = Mathf.Max(Mathf.Max(drawPosA.y, drawPosB.y) - drawPosC.y, 0f) + 0.5f;
      
      var door = enterSpot.Cell.GetDoor(enterSpot.Map);
      var ramp = enterSpot.Cell.GetFirstThing<Building_VehicleRamp>(enterSpot.Map);
      door?.StartManualOpenBy(toil.actor);
      ramp?.StartManualOpenBy(toil.actor);

      var rect = Rect.MinMaxRect(drawPosA.x, drawPosA.z, drawPosB.x, drawPosB.z);
      // 裏返りを修正
      if (rect.xMin > rect.xMax) (rect.xMin, rect.xMax) = (rect.xMax, rect.xMin);
      if (rect.yMin > rect.yMax) (rect.yMin, rect.yMax) = (rect.yMax, rect.yMin);
      if (distanceSquared < 0.05f || !rect.ExpandedBy(1f).Contains(toil.actor.DrawPos.ToVector2()))
      {
        driver.ReadyForNextToil();
      }
    };
    toil.AddFinishAction(() => driver.drawOffset = Vector3.zero);
    toil.FailOn(() => comp is { parent.Spawned: false } or { AvailableAccessSpot.IsValid: false });
    toil.defaultCompleteMode = ToilCompleteMode.Never;
    return toil;

    //なんかUnityのnormalizedって重いらしいよ
    static Vector3 NormalizeFlat(Vector3 vec)
    {
      var length = vec.MagnitudeHorizontal();
      return new Vector3(vec.x / length, 0f, vec.z / length);
    }
  }

  //デスポーン後目的地のマップにリスポーン。スポーン地の再計算時にそこが埋まってたらとりあえず失敗に
  private static Toil TraverseToil(TargetInfo enterSpot, CompVehicleEnterSpot comp)
  {
    var toil = ToilMaker.MakeToil();
    toil.defaultCompleteMode = ToilCompleteMode.Instant;
    toil.initAction = () =>
    {
      IntVec3 cell;
      Map map;
      var rot = toil.actor.Rotation;
      var vehiclePawn = toil.actor as VehiclePawn;
      if (toil.actor.Map != enterSpot.Map)
      {
        cell = enterSpot.Cell;
        map = enterSpot.Map;
      }
      else if (comp is not null)
      {
        if (comp.AvailableAccessSpot is not { IsValid: true } accessSpot) return;
        cell = accessSpot.Cell;
        map = accessSpot.Map;
      }
      else
      {
        cell = CrossMapReachabilityUtility.EnterVehiclePosition(enterSpot, vehiclePawn);
        map = enterSpot.Map.GroundMap;
      }

      if (!cell.IsValid) return;

      if (vehiclePawn is not null)
      {
        vehiclePawn.DeSpawnWithoutJobClearVehicle();
        GenSpawn.Spawn(toil.actor, cell + (rot.FacingCell * vehiclePawn.HalfLength()), map, rot);
      }
      else
      {
        toil.actor.DeSpawnWithoutJobClear();
        if (toil.actor.roping != null)
        {
          foreach (var ropee in toil.actor.roping.Ropees)
          {
            ropee.DeSpawnWithoutJobClear();
          }
        }

        GenSpawn.Spawn(toil.actor, cell, map, rot, WipeMode.VanishOrMoveAside);
        if (toil.actor.roping != null)
        {
          foreach (var ropee in toil.actor.roping.Ropees)
          {
            GenSpawn.Spawn(ropee, cell, map, rot, WipeMode.VanishOrMoveAside);
          }
        }
      }
    };
    return toil.FailOn(() => enterSpot is not { Cell.IsValid: true, Map.Disposed: false });
  }
}