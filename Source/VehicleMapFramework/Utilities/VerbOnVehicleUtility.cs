using System.Collections.Generic;
using RimWorld;
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
        public bool TryFindShootLineFromToOnVehicle(IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, bool ignoreRange = false)
        {
            resultingLine = default;
            var flag = verb.caster.IsOnVehicleMapOf(out var vehicle) && verb is not (Verb_Jump or Verb_CastAbilityJump or Verb_LaunchZipline);
            var flag2 = targ.Thing.IsOnVehicleMapOf(out var vehicle2);
            VehiclePawnWithMap vehicle3 = null;
            var flag3 = verb.caster.TryGetTargetMap(out var map) && map.IsVehicleMapOf(out vehicle3);
            //if (!flag && !flag2 && !flag3)
            //{
            //    return verb.TryFindShootLineFromTo(root, targ, out resultingLine, ignoreRange);
            //}
            if (root == verb.caster.Position)
            {
                root = verb.caster.PositionOnBaseMapSpawned;
            }

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
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return false;
            }

            if (verb.verbProps.IsMeleeAttack || verb.EffectiveRange <= 1.42f)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return ReachabilityImmediate.CanReachImmediate(verb.caster.Position, targ, verb.caster.Map,
                    PathEndMode.Touch, null);
            }

            var occupiedRect =
                targ.HasThing ? targ.Thing.MovedOccupiedRect() : CellRect.SingleCell(targCellOnBaseMap);
            if (!ignoreRange && verb.OutOfRange(root, targ, occupiedRect))
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return false;
            }

            if (!verb.verbProps.requireLineOfSight)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return true;
            }

            if (verb.CasterIsPawn)
            {
                if (verb.CanHitFromCellIgnoringRange(root, targ, out var dest))
                {
                    resultingLine = new ShootLine(root, dest);
                    return true;
                }

                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(verb.caster.Position,
                    occupiedRect.ClosestCellTo(root), verb.caster.Map, tempLeanShootSources);
                for (var i = 0; i < tempLeanShootSources.Count; i++)
                {
                    var intVec = tempLeanShootSources[i].ToThingBaseMapCoord(verb.caster);
                    if (verb.CanHitFromCellIgnoringRange(intVec, targ, out dest))
                    {
                        resultingLine = new ShootLine(intVec, dest);
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
                        resultingLine = new ShootLine(intVec2, dest);
                        return true;
                    }
                }
            }

            resultingLine = new ShootLine(root, targCellOnBaseMap);
            return false;
        }

        public bool CanHitFromCellIgnoringRange(IntVec3 sourceCellBaseCol, LocalTargetInfo targ, out IntVec3 goodDest)
        {
            var targCellOnBaseMap = targ.TargetCellOnBaseMap(verb.caster);
            if (targ.HasThing)
            {
                if (targ.Thing.BaseMapOrCaravan != verb.caster.BaseMapOrCaravan)
                {
                    goodDest = IntVec3.Invalid;
                    return false;
                }
                ShootLeanUtilityOnVehicle.CalcShootableCellsOf(tempDestList, targ.Thing, sourceCellBaseCol);
                var intVec =  sourceCellBaseCol.ToThingMapCoord(targ.Thing);
                for (var i = 0; i < tempDestList.Count; i++)
                {
                    if (verb.CanHitCellFromCellIgnoringRange(intVec, tempDestList[i], targ.Thing.Map, targ.Thing.def.Fillage == FillCategory.Full))
                    {
                        goodDest = tempDestList[i].ToThingBaseMapCoord(targ.Thing);
                        return true;
                    }
                }
            }
            else if (verb.CanHitCellFromCellIgnoringRange(sourceCellBaseCol, targCellOnBaseMap, verb.Caster.BaseMap()))
            {
                goodDest = targCellOnBaseMap;
                return true;
            }
            goodDest = IntVec3.Invalid;
            return false;
        }

        private bool CanHitCellFromCellIgnoringRange(IntVec3 sourceSq, IntVec3 targetLoc, Map map, bool includeCorners = false)
        {
            if (verb.verbProps.mustCastOnOpenGround && (!targetLoc.Standable(map) || map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
            {
                return false;
            }
            if (verb.verbProps.requireLineOfSight)
            {
                if (!includeCorners)
                {
                    if (!GenSightOnVehicle.LineOfSight(sourceSq, targetLoc, map, false))
                    {
                        return false;
                    }
                }
                else if (!GenSightOnVehicle.LineOfSightToEdges(sourceSq, targetLoc, map))
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