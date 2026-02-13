using System.Linq;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class Ability_GrapplingHook : Ability
{
    public Ability_GrapplingHook()
    {
    }

    public Ability_GrapplingHook(Pawn pawn) : base(pawn)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, AbilityDef def) : base(pawn, def)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def)
    {
    }

    public override AcceptanceReport CanCast
    {
        get
        {
            var canCast = base.CanCast;
            if (!canCast.Accepted || verb is not Verb_LaunchZipline launchVerb)
                return canCast;
            return launchVerb.ziplineEnd is not null
                ? "VMF_GrapplingHookAlreadyLaunched".Translate()
                : AcceptanceReport.WasAccepted;
        }
    }
    
    private bool ValidAOEAffectedTarget(Thing target)
    {
        if (!this.verb.targetParams.CanTarget(target))
        {
            return false;
        }
        return !target.Fogged() && EffectComps.All(t => t.Valid((LocalTargetInfo)target));
    }

    public bool TryFindCastPosition(TargetInfo destination, out TargetInfo castSpot, out TargetInfo targSpot)
    {
        return TryFindCastPositionFromTo(new TargetInfo(pawn.Position, pawn.Map), destination, out castSpot, out targSpot);
    }

    public bool TryFindCastPositionFromTo(TargetInfo from, TargetInfo to, out TargetInfo castSpot, out TargetInfo targSpot)
    {
        castSpot = TargetInfo.Invalid;
        targSpot = TargetInfo.Invalid;
        
        var map = from.Map;
        var map2 = to.Map;
        if (map is null || map == map2)
            return false;

        var toVehicleMap = map2.IsVehicleMapOf(out var vehicle2);
        var minRange = verb.verbProps.EffectiveMinRange(true);
        var maxRange = verb.EffectiveRange;
        
        IntVec3 closestCell;
        if (map.IsVehicleMapOf(out var vehicle))
        {
            closestCell = ClosestCell(to.CellOnBaseMap(), vehicle);
        }
        else if (toVehicleMap)
        {
            closestCell = ClosestCell(from.CellOnBaseMap(), vehicle2);
            if (closestCell.IsValid) closestCell = closestCell.ToBaseMapCoord(vehicle2);
        }
        else closestCell = IntVec3.Invalid;
        if (!closestCell.IsValid) return false;
        
        var castPosition = CrossMapRCellFinder.GoodDestNearFromTo(from.Cell, closestCell, pawn, map, radius: maxRange);
        if (!castPosition.IsValid) return false;
        var castPositionOnBaseMap = castPosition.ToBaseMapCoord(map);
        var castPositionOnTargMap = castPositionOnBaseMap.ToVehicleMapCoord(map2);

        var targetParams = verb.targetParams;
        var canTargetLocations = targetParams.canTargetLocations;
        var canTargetThings = targetParams.canTargetPawns || targetParams.canTargetBuildings || targetParams.canTargetItems ||
                              targetParams.canTargetPlants || targetParams.canTargetSelf || targetParams.canTargetFires;

        var tmpTargetMap = pawn.TargetMap;
        var flag = tmpTargetMap != map2;
        if (flag) pawn.TargetMap = map2;

        var rect = toVehicleMap ? vehicle2.ValidMapRect : map2.BoundsRect(1);
        var pattern = GenRadialDirectional.PatternFor(castPositionOnTargMap, rect, minRange, maxRange, out var indexRange);
        var minRangeSquared = minRange * minRange;
        for (var i = indexRange.min; i < indexRange.max; i++)
        {
            var cell = castPositionOnTargMap + pattern[i];
            if (!cell.InBounds(map2) || pattern[i].LengthHorizontalSquared < minRangeSquared) continue;

            if (canTargetLocations)
            {
                if (verb.ValidateTarget(cell, false) && verb.CanHitTargetFrom(castPositionOnBaseMap, cell))
                {
                    castSpot = new TargetInfo(castPosition, map);
                    targSpot = new TargetInfo(cell, map2);
                    if (flag) pawn.TargetMap = tmpTargetMap;
                    return true;
                }
            }
            if (canTargetThings)
            {
                foreach (var thing in map2.thingGrid.ThingsListAtFast(cell))
                {
                    if (ValidAOEAffectedTarget(thing))
                    {
                        castSpot = new TargetInfo(castPosition, map);
                        targSpot = thing;
                        if (flag) pawn.TargetMap = tmpTargetMap;
                        return true;
                    }
                }
            }
        }
        
        if (flag) pawn.TargetMap = tmpTargetMap;
        return false;

        static IntVec3 ClosestCell(IntVec3 cellOnBaseMap, VehiclePawnWithMap vehicle)
        {
            if (vehicle.CachedWalkableMapEdgeCells.Count == 0) return IntVec3.Invalid;
            
            var cellOnVehicleMap = cellOnBaseMap.ToVehicleMapCoord(vehicle);
            var mapRect = vehicle.ValidMapRect.ExpandedBy(1);
            var root = mapRect.ClosestCellTo(cellOnVehicleMap);
            var radius = (mapRect.GetCorner(Rot4.North) - mapRect.GetCorner(Rot4.South)).LengthHorizontal;
            
            var pattern =
                GenRadialDirectional.PatternFor(cellOnVehicleMap, vehicle.ValidMapRect, 0f, radius, out var indexRange);
            for (var i = indexRange.min; i < indexRange.max; i++)
            {
                var cell = root + pattern[i];
                if (vehicle.CachedWalkableMapEdgeCells.Contains(cell))
                    return cell;
            }

            return IntVec3.Invalid;
        }
    }

    public virtual void OnHit(ZiplineEnd ziplineEnd)
    {
        pawn.TargetMap = ziplineEnd.Map;
        JumpUtility.DoJump(pawn, ziplineEnd, null, verb.verbProps, this, ziplineEnd, VMF_DefOf.VMF_GrapplingHookFlyer);
        if (verb is Verb_LaunchZipline verbLaunchZipline)
            verbLaunchZipline.ziplineEnd = null;
    }
}