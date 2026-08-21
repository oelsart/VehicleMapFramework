using System.Linq;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public abstract class Ability_MapTraverse : Ability
{
  protected Ability_MapTraverse()
  {
  }

  protected Ability_MapTraverse(Pawn pawn) : base(pawn)
  {
  }

  protected Ability_MapTraverse(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept)
  {
  }

  protected Ability_MapTraverse(Pawn pawn, AbilityDef def) : base(pawn, def)
  {
  }

  protected Ability_MapTraverse(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def)
  {
  }

  private bool ValidAOEAffectedTarget(Thing target)
  {
    if (!verb.targetParams.CanTarget(target))
    {
      return false;
    }

    return !target.Fogged() && EffectComps.All(t => t.Valid((LocalTargetInfo)target));
  }

  public virtual bool TryFindCastPosition(TargetInfo destination, out TargetInfo castSpot, out TargetInfo targSpot)
  {
    return TryFindCastPositionFromTo(new TargetInfo(pawn.Position, pawn.Map), destination, out castSpot, out targSpot);
  }

  public virtual bool TryFindCastPositionFromTo(TargetInfo from, TargetInfo to, out TargetInfo castSpot,
    out TargetInfo targSpot, int districtID = -1)
  {
    castSpot = TargetInfo.Invalid;
    targSpot = TargetInfo.Invalid;

    var fromMap = from.Map;
    var toMap = to.Map;
    if (fromMap is null || fromMap == toMap)
      return false;

    var toVehicleMap = toMap.IsVehicleMapOf(out var vehicle2);
    var minRange = verb.verbProps.EffectiveMinRange(true);
    var maxRange = verb.EffectiveRange;

    IntVec3 closestCell;
    // ターゲットに最も近いfrom.Map上のセルを特定
    if (fromMap.IsVehicleMapOf(out var vehicle))
    {
      closestCell = to.CellOnGroundMap.ClosestWalkableEdgeCell(vehicle);
      // to.Mapとの距離による早期終了判定
      if (closestCell.IsValid && toVehicleMap)
      {
        var closestCell2 = closestCell.ToBaseMapCoord(vehicle).ToVehicleMapCoord(vehicle2);
        var distSquared = (vehicle2.ValidMapRect.ClosestCellTo(closestCell2) - closestCell2).LengthHorizontalSquared;
        if (distSquared > maxRange * maxRange) return false;
      }
    }
    else if (toVehicleMap)
    {
      closestCell = from.CellOnGroundMap.ClosestWalkableEdgeCell(vehicle2);
      if (closestCell.IsValid)
      {
        closestCell = closestCell.ToBaseMapCoord(vehicle2);
        // to.Mapとの距離による早期終了判定
        var closestCell2 = closestCell.ToVehicleMapCoord(vehicle2);
        var distSquared = (vehicle2.ValidMapRect.ClosestCellTo(closestCell2) - closestCell2).LengthHorizontalSquared;
        if (distSquared > maxRange * maxRange) return false;
      }
    }
    else closestCell = IntVec3.Invalid;

    if (!closestCell.IsValid) return false;

    var castPosition =
      CrossMapRCellFinder.GoodDestNearFromTo(from.Cell, closestCell, pawn, fromMap, reserve: false, radius: maxRange);
    if (!castPosition.IsValid) return false;
    var castPositionOnBaseMap = castPosition.ToBaseMapCoord(fromMap);
    var castPositionOnTargMap = castPositionOnBaseMap.ToVehicleMapCoord(toMap);

    var targetParams = verb.targetParams;
    var canTargetLocations = targetParams.canTargetLocations;
    var canTargetThings = targetParams.canTargetPawns || targetParams.canTargetBuildings ||
                          targetParams.canTargetItems ||
                          targetParams.canTargetPlants || targetParams.canTargetSelf || targetParams.canTargetFires;

    var tmpTargetMap = pawn.TargetMap;
    var flag = tmpTargetMap != toMap;
    if (flag) pawn.TargetMap = toMap;

    var rect = toVehicleMap ? vehicle2.ValidMapRect : toMap.BoundsRect(1);
    var pattern = GenRadialDirectional.PatternFor(castPositionOnTargMap, rect, minRange, maxRange, out var indexRange);
    for (var i = indexRange.min; i < indexRange.max; i++)
    {
      var cell = castPositionOnTargMap + pattern[i];
      if (!rect.Contains(cell)) continue;

      if (canTargetLocations)
      {
        if (verb.ValidateTarget(cell, false) && verb.CanHitTargetFrom(castPositionOnBaseMap, cell) &&
            (districtID == -1 || RegionAndRoomQuery.DistirctAtFast(cell, toMap)?.ID == districtID))
        {
          castSpot = new TargetInfo(castPosition, fromMap);
          targSpot = new TargetInfo(cell, toMap);
          if (flag) pawn.TargetMap = tmpTargetMap;
          return true;
        }
      }

      if (canTargetThings)
      {
        foreach (var thing in toMap.thingGrid.ThingsListAtFast(cell))
        {
          if (ValidAOEAffectedTarget(thing) &&
              (districtID == -1 || RegionAndRoomQuery.DistirctAtFast(cell, toMap)?.ID == districtID))
          {
            castSpot = new TargetInfo(castPosition, fromMap);
            targSpot = thing;
            if (flag) pawn.TargetMap = tmpTargetMap;
            return true;
          }
        }
      }
    }

    if (flag) pawn.TargetMap = tmpTargetMap;
    return false;
  }
}