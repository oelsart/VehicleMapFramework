using CombatExtended;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleInteriors;
using Verse;
using Verse.AI;

namespace VMF_CEPatch
{
    public static class VerbOnVehicleCEUtility
    {
        private static Vector3 ShotSource(this Verb_LaunchProjectileCE verb)
        {
            var drawPos = verb.caster.DrawPos;
            return new Vector3(drawPos.x, verb.ShotHeight, drawPos.z);
        }

        public static bool TryFindCEShootLineFromToOnVehicle(this Verb_LaunchProjectileCE verb, IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine)
        {
            var casterBaseMap = verb.caster.BaseMap();
            var targCellOnBaseMap = targ.CellOnBaseMap();
            if (targ.HasThing && targ.Thing.BaseMap() != casterBaseMap)
            {
                resultingLine = default;
                return false;
            }
            if (verb.EffectiveRange <= ShootTuning.MeleeRange)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return ReachabilityImmediate.CanReachImmediate(verb.caster.Position, targ, verb.caster.Map, PathEndMode.Touch, null);
            }
            CellRect cellRect = (!targ.HasThing) ? CellRect.SingleCell(targ.Cell) : targ.Thing.MovedOccupiedRect();
            float num = cellRect.ClosestDistSquaredTo(root);
            if (num > verb.EffectiveRange * verb.EffectiveRange || num < verb.verbProps.minRange * verb.verbProps.minRange)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return false;
            }
            if (verb.Projectile.projectile.flyOverhead)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return true;
            }

            var shotSource = root.ToVector3Shifted();
            shotSource.y = verb.ShotHeight;

            // Adjust for multi-tile turrets
            if (verb.caster.def.building?.IsTurret ?? false)
            {
                shotSource = verb.ShotSource();
            }

            if (verb.CanHitFromCellIgnoringRange(shotSource, targ, out IntVec3 dest))
            {
                resultingLine = new ShootLine(root, dest);
                return true;
            }

            if (verb.CasterIsPawn)
            {
                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(verb.caster.Position, cellRect.ClosestCellTo(root), verb.caster.Map, tempLeanShootSources);
                var targCellOnCasterMap = targ.CellOnAnotherThingMap(verb.caster);
                foreach (var leanLoc in tempLeanShootSources.OrderBy(c => c.DistanceTo(targCellOnCasterMap)))
                {
                    var leanOffset = 0.5f - 0.001f;
                    var leanLocOnBaseMap = leanLoc.OrigToThingMap(verb.caster);
                    var leanPosOffset = (leanLocOnBaseMap - root).ToVector3() * leanOffset;
                    if (verb.CanHitFromCellIgnoringRange(shotSource + leanPosOffset, targ, out dest))
                    {
                        resultingLine = new ShootLine(leanLocOnBaseMap, dest);
                        return true;
                    }
                }
            }

            resultingLine = new ShootLine(root, targCellOnBaseMap);
            return false;
        }

        private static bool CanHitFromCellIgnoringRange(this Verb_LaunchProjectileCE verb, Vector3 shotSource, LocalTargetInfo targ, out IntVec3 goodDest)
        {
            var targCellOnBaseMap = targ.CellOnBaseMap();
            var baseMap = verb.Caster.BaseMap();
            if (targ.Thing != null && targ.Thing.BaseMap() != baseMap)
            {
                goodDest = IntVec3.Invalid;
                return false;
            }
            if (targ.HasThing)
            {
                if (verb.CanHitCellFromCellIgnoringRange(shotSource, targ.Cell, targ.Thing.Map, targ.Thing))
                {
                    goodDest = targCellOnBaseMap;
                    return true;
                }
            }
            else if (verb.CanHitCellFromCellIgnoringRange(shotSource, targCellOnBaseMap, baseMap))
            {
                goodDest = targCellOnBaseMap;
                return true;
            }

            goodDest = IntVec3.Invalid;
            return false;
        }

        private static bool CanHitCellFromCellIgnoringRange(this Verb_LaunchProjectileCE verb, Vector3 shotSource, IntVec3 targetLoc, Map map, Thing targetThing = null)
        {
            if (verb.verbProps.mustCastOnOpenGround && (!targetLoc.Standable(map) || map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
            {
                return false;
            }
            if (verb.verbProps.requireLineOfSight)
            {
                // Calculate shot vector
                Vector3 targetPos;
                if (targetThing != null)
                {
                    float shotHeight = shotSource.y;
                    verb.AdjustShotHeight(verb.caster, targetThing, ref shotHeight);
                    shotSource.y = shotHeight;
                    Vector3 targDrawPos = targetThing.DrawPos;
                    targetPos = new Vector3(targDrawPos.x, new CollisionVertical(targetThing).Max, targDrawPos.z);
                    var targPawn = targetThing as Pawn;
                    if (targPawn != null)
                    {
                        targetPos += targPawn.Drawer.leaner.LeanOffset * 0.6f;
                    }
                }
                else
                {
                    targetPos = targetLoc.ToVector3Shifted();
                    if (map.IsVehicleMapOf(out var vehicle))
                    {
                        targetPos = targetPos.OrigToVehicleMap(vehicle);
                    }
                }
                Ray shotLine = new Ray(shotSource, (targetPos - shotSource));

                // Create validator to check for intersection with partial cover
                var aimMode = verb.CompFireModes?.CurrentAimMode;

                Predicate<IntVec3> CanShootThroughCell = (IntVec3 cell) =>
                {
                    Thing cover = cell.InBounds(map) ? cell.GetFirstPawn(map) ?? cell.GetCover(map) : null;
                    if (verb.caster.IsOnVehicleMapOf(out var vehicle) && cover == vehicle)
                    {
                        return true;
                    }

                    if (cover != null && cover != verb.ShooterPawn && cover != verb.caster && cover != targetThing && !cover.IsPlant() && !(cover is Pawn && cover.HostileTo(verb.caster)))
                    {
                        //Shooter pawns don't attempt to shoot targets partially obstructed by their own faction members or allies, except when close enough to fire over their shoulder
                        if (cover is Pawn cellPawn && !cellPawn.Downed && cellPawn.Faction != null && verb.ShooterPawn?.Faction != null && (verb.ShooterPawn.Faction == cellPawn.Faction || verb.ShooterPawn.Faction.RelationKindWith(cellPawn.Faction) == FactionRelationKind.Ally) && !cellPawn.AdjacentTo8WayOrInside(verb.ShooterPawn))
                        {
                            return false;
                        }

                        // Skip this check entirely if we're doing suppressive fire and cell is adjacent to target
                        if ((verb.VerbPropsCE.ignorePartialLoSBlocker || aimMode == AimMode.SuppressFire) && cover.def.Fillage != FillCategory.Full)
                        {
                            return true;
                        }

                        Bounds bounds = CE_Utility.GetBoundsFor(cover);

                        // Simplified calculations for adjacent cover for gameplay purposes
                        if (cover.def.Fillage != FillCategory.Full && cover.AdjacentTo8WayOrInside(verb.caster))
                        {
                            // Sanity check to prevent stuff behind us blocking LoS
                            var cellTargDist = cell.DistanceTo(targetLoc);
                            var shotTargDist = shotSource.ToIntVec3().DistanceTo(targetPos.ToIntVec3());

                            if (shotTargDist > cellTargDist)
                            {
                                return cover is Pawn || bounds.size.y < shotSource.y;
                            }
                        }

                        // Check for intersect
                        if (bounds.IntersectRay(shotLine))
                        {
                            if (Controller.settings.DebugDrawPartialLoSChecks)
                            {
                                verb.caster.BaseMap().debugDrawer.FlashCell(cell, 0, bounds.extents.y.ToString());
                            }
                            return false;
                        }

                        if (Controller.settings.DebugDrawPartialLoSChecks)
                        {
                            verb.caster.BaseMap().debugDrawer.FlashCell(cell, 0.7f, bounds.extents.y.ToString());
                        }
                    }

                    return true;
                };

                // Add validator to parameters
                foreach (IntVec3 curCell in GenSightCE.PointsOnLineOfSight(shotSource, targetPos))
                {
                    if (Controller.settings.DebugDrawPartialLoSChecks)
                    {
                        verb.caster.BaseMap().debugDrawer.FlashCell(curCell, 0.4f);
                    }
                    if (curCell != shotSource.ToIntVec3() && curCell != targetLoc && !CanShootThroughCell(targetThing != null ? curCell.ThingMapToOrig(targetThing) : curCell))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();
    }
}
