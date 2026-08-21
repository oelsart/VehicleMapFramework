using System.Collections.Generic;
using SmashTools;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class VerbOnVehicleUtility
{
  private static readonly List<Thing> cellThingsFiltered = [];

  private static readonly List<IntVec3> tempLeanShootSources = [];

  private static readonly List<IntVec3> tempDestList = [];

  extension(Verb verb)
  {
    public bool TryFindShootLineFromToOnVehicle(IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine,
      bool ignoreRange = false)
    {
      resultingLine = default;
      var flag = verb.caster.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned;
      var flag2 = targ.Thing.IsOnVehicleMapOf(out var vehicle2) && vehicle2.Spawned;
      VehiclePawnWithMap vehicle3 = null;
      var flag3 = verb.caster.TryGetTargetMap(out var map) && map.IsVehicleMapOf(out vehicle3);
      //if (!flag && !flag2 && !flag3)
      //{
      //    return verb.TryFindShootLineFromTo(root, targ, out resultingLine, ignoreRange);
      //}
      var positionOnBaseMap = verb.caster.PositionOnBaseMap;
      var tmpRoot = !flag ? root : positionOnBaseMap;
      var casterBaseMap = verb.caster.BaseMap();
      var targCellOnBaseMap = targ.TargetCellOnBaseMap(verb.caster);

      if (targ.HasThing && targ.Thing.BaseMapOrCaravan != verb.caster.BaseMapOrCaravan)
      {
        return false;
      }

      // 車両マップの下から上や上から下への射線は通らないものとする
      if (flag && !flag2 && targ.Cell.InBounds(casterBaseMap) &&
          targ.Cell.TryGetVehicleMap(casterBaseMap, out var vehicle4) && vehicle4 == vehicle2 ||
          !flag && flag2 && verb.caster.Position.TryGetVehicleMap(casterBaseMap, out vehicle4) &&
          vehicle4 == vehicle ||
          !flag && flag3 && verb.caster.Position.TryGetVehicleMap(casterBaseMap, out vehicle4) &&
          vehicle4 == vehicle3)
      {
        resultingLine = new ShootLine(tmpRoot, targCellOnBaseMap);
        return false;
      }

      if (verb.verbProps.IsMeleeAttack || verb.EffectiveRange <= 1.42f)
      {
        resultingLine = new ShootLine(tmpRoot, targCellOnBaseMap);
        return ReachabilityImmediate.CanReachImmediate(verb.caster.Position, targ, verb.caster.Map,
          PathEndMode.Touch, null);
      }

      var occupiedRect =
        targ.HasThing ? targ.Thing.MovedOccupiedRect() : CellRect.SingleCell(targCellOnBaseMap);
      if (!ignoreRange && verb.OutOfRange(positionOnBaseMap, targ, occupiedRect))
      {
        resultingLine = new ShootLine(tmpRoot, targCellOnBaseMap);
        return false;
      }

      if (!verb.verbProps.requireLineOfSight)
      {
        resultingLine = new ShootLine(tmpRoot, targCellOnBaseMap);
        return true;
      }

      if (verb.CasterIsPawn)
      {
        if (verb.CanHitFromCellIgnoringRange(tmpRoot, targ, out var dest))
        {
          resultingLine = new ShootLine(tmpRoot, dest);
          return true;
        }

        var sourceBand = AsAboveSoBelow.GetTargetBand(verb.caster);
        var targetBand = AsAboveSoBelow.GetTargetBand(targ.Thing);
        ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(verb.caster.Position,
          occupiedRect.ClosestCellTo(positionOnBaseMap), verb.caster.Map, tempLeanShootSources, sourceBand, targetBand);
        for (var i = 0; i < tempLeanShootSources.Count; i++)
        {
          var intVec = tempLeanShootSources[i].ToThingBaseMapCoord(verb.caster);
          if (verb.CanHitFromCellIgnoringRange(intVec, targ, out dest))
          {
            resultingLine = new ShootLine(!flag ? tempLeanShootSources[i] : intVec, dest);
            return true;
          }
        }
      }
      else
      {
        foreach (var intVec2 in verb.Caster.MovedOccupiedRect())
        {
          if (verb.CanHitFromCellIgnoringRange(intVec2, targ, out var dest))
          {
            resultingLine = new ShootLine(!flag ? intVec2.ToThingMapCoord(verb.caster) : intVec2, dest);
            return true;
          }
        }
      }

      resultingLine = new ShootLine(tmpRoot, targCellOnBaseMap);
      return false;
    }

    public bool CanHitFromCellIgnoringRange(IntVec3 sourceCellBaseCol, LocalTargetInfo targ, out IntVec3 goodDest)
    {
      var targCellOnBaseMap = targ.TargetCellOnBaseMap(verb.caster);
      var sourceBand = AsAboveSoBelow.GetTargetBand(verb.caster);
      if (targ.HasThing)
      {
        var targetBand = AsAboveSoBelow.GetTargetBand(targ.Thing);
        if (targ.Thing.BaseMapOrCaravan != verb.caster.BaseMapOrCaravan)
        {
          goodDest = IntVec3.Invalid;
          return false;
        }

        ShootLeanUtilityOnVehicle.CalcShootableCellsOf(tempDestList, targ.Thing, sourceCellBaseCol, sourceBand, targetBand);
        var intVec = sourceCellBaseCol.ToThingMapCoord(targ.Thing);
        for (var i = 0; i < tempDestList.Count; i++)
        {
          if (verb.CanHitCellFromCellIgnoringRange(intVec, tempDestList[i], targ.Thing.Map,
                sourceBand, targetBand, targ.Thing.def.Fillage == FillCategory.Full))
          {
            goodDest = tempDestList[i].ToThingBaseMapCoord(targ.Thing);
            return true;
          }
        }
      }
      else if (verb.CanHitCellFromCellIgnoringRange(sourceCellBaseCol, targCellOnBaseMap, verb.Caster.BaseMap(), sourceBand, null))
      {
        goodDest = targCellOnBaseMap;
        return true;
      }

      goodDest = IntVec3.Invalid;
      return false;
    }

    private bool CanHitCellFromCellIgnoringRange(IntVec3 sourceSq, IntVec3 targetLoc, Map map,
      AsAboveSoBelow.TargetBand? sourceBand, AsAboveSoBelow.TargetBand? targetBand, bool includeCorners = false)
    {
      if (verb.verbProps.mustCastOnOpenGround &&
          (!targetLoc.Standable(map) || map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
      {
        return false;
      }

      if (verb.verbProps.requireLineOfSight)
      {
        if (!includeCorners)
        {
          if (!GenSightOnVehicle.LineOfSight(sourceSq, targetLoc, map, sourceBand, targetBand))
          {
            return false;
          }
        }
        else if (!GenSightOnVehicle.LineOfSightToEdges(sourceSq, targetLoc, map, sourceBand, targetBand))
        {
          return false;
        }
      }

      return true;
    }
  }

  private static readonly List<IntVec3> tmpCellList = [];

  public static bool ShouldConsiderCrossMap(Thing caster, IntVec3 root, LocalTargetInfo targ)
  {
    if (!root.IsValid || !caster.Spawned ||
        VehiclePawnWithMapCache.AllVehiclesOn(caster.GroundMap).Count == 0) return false;

    if ((caster.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned ||
         targ.Thing.IsOnVehicleMapOf(out vehicle) && vehicle.Spawned ||
         (caster.TryGetTargetMap(out var map) && map.IsVehicleMapOf(out vehicle) && vehicle.Spawned)))
      return true;

    var casterMap = caster.Map;
    var component = casterMap?.GetCachedMapComponent<VehicleMapGrid>();
    if (component is null) return false;

    tmpCellList.Clear();
    GenSight.PointsOnLineOfSight(root, targ.Cell, c => tmpCellList.Add(c));
    foreach (var cell in tmpCellList.AsReadOnlySpan())
    {
      if (cell.InBounds(casterMap) && component.VehicleAt(cell) is not null)
        return true;
    }

    return false;
  }
}