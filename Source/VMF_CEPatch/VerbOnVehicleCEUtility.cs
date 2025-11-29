using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class VerbOnVehicleCEUtility
{
    private static readonly List<IntVec3> tempLeanShootSources = [];

    extension(Verb_LaunchProjectileCE verb)
    {
        private Vector3 ShotSource()
        {
            var drawPos = verb.caster.DrawPos;
            var shotHeight = verb.ShotHeight;
            if (verb.caster.IsOnVehicleMapOf(out var vehicle))
            {
                shotHeight = shotHeight.YOffsetFull(vehicle);
            }
            return new Vector3(drawPos.x, shotHeight, drawPos.z);
        }

        public bool TryFindCEShootLineFromToOnVehicle(IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, out Vector3 targetPos)
        {
            targetPos = targ.Thing is Pawn ? targ.Thing.TrueCenter() : TargetMapManager.TargetCellOnBaseMap(ref targ, verb.caster).ToVector3Shifted();
            var targCellOnBaseMap = TargetMapManager.TargetCellOnBaseMap(ref targ, verb.caster);

            if (targ.HasThing && targ.Thing.BaseMapOrCaravan() != verb.caster.BaseMapOrCaravan())
            {
                resultingLine = default;
                return false;
            }
            if (verb.EffectiveRange <= ShootTuning.MeleeRange)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return ReachabilityImmediate.CanReachImmediate(verb.caster.Position, targ, verb.caster.Map, PathEndMode.Touch, null);
            }
            var cellRect = !targ.HasThing ? CellRect.SingleCell(targ.Cell) : targ.Thing.MovedOccupiedRect();
            var num = cellRect.ClosestDistSquaredTo(root);
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

            if (verb.CanHitFromCellIgnoringRange(shotSource, targ, out var dest))
            {
                targetPos = dest.ToVector3Shifted();
                resultingLine = new ShootLine(root, dest);
                return true;
            }

            if (verb.CasterIsPawn)
            {
                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(verb.caster.Position, cellRect.ClosestCellTo(root), verb.caster.Map, tempLeanShootSources);
                var targCellOnCasterMap = targ.CellOnAnotherThingMap(verb.caster);
                foreach (var leanLoc in tempLeanShootSources.OrderBy(c => c.DistanceTo(targCellOnCasterMap)))
                {
                    const float leanOffset = 0.5f - 0.001f;
                    var leanLocOnBaseMap = leanLoc.ToThingBaseMapCoord(verb.caster);
                    var leanPosOffset = (leanLocOnBaseMap - root).ToVector3() * leanOffset;
                    if (verb.CanHitFromCellIgnoringRange(shotSource + leanPosOffset, targ, out dest))
                    {
                        targetPos = dest.ToVector3Shifted();
                        resultingLine = new ShootLine(leanLocOnBaseMap, dest);
                        return true;
                    }
                }
            }

            resultingLine = new ShootLine(root, targCellOnBaseMap);
            return false;
        }

        private bool CanHitFromCellIgnoringRange(Vector3 shotSource, LocalTargetInfo targ, out IntVec3 goodDest)
        {
            var targCellOnBaseMap = targ.CellOnBaseMap();
            if (targ.Thing != null && targ.Thing.BaseMapOrCaravan() != verb.Caster.BaseMapOrCaravan())
            {
                goodDest = IntVec3.Invalid;
                return false;
            }
            if (targ.HasThing)
            {
                if (verb.CanHitCellFromCellIgnoringRange(shotSource, targ.Cell, targ.Thing!.Map, targ.Thing))
                {
                    goodDest = targCellOnBaseMap;
                    return true;
                }
            }
            else if (verb.CanHitCellFromCellIgnoringRange(shotSource, targCellOnBaseMap, verb.Caster.BaseMap()))
            {
                goodDest = targCellOnBaseMap;
                return true;
            }

            goodDest = IntVec3.Invalid;
            return false;
        }

        private bool CanHitCellFromCellIgnoringRange(Vector3 shotSource, IntVec3 targetLoc, Map map, Thing targetThing = null)
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
                    var shotHeight = shotSource.y;
                    verb.AdjustShotHeight(verb.caster, targetThing, ref shotHeight);
                    shotSource.y = shotHeight;
                    var targDrawPos = targetThing.DrawPos;
                    targetPos = new Vector3(targDrawPos.x, new CollisionVertical(targetThing).Max, targDrawPos.z);
                    if (targetThing is Pawn targPawn)
                    {
                        targetPos += targPawn.Drawer.leaner.LeanOffset * 0.6f;
                    }
                }
                else
                {
                    targetPos = targetLoc.ToVector3Shifted();
                    if (map.IsVehicleMapOf(out var vehicle))
                    {
                        targetPos = targetPos.ToBaseMapCoord(vehicle);
                    }
                }
                Ray shotLine = new(shotSource, targetPos - shotSource);

                // Create validator to check for intersection with partial cover
                var aimMode = verb.CompFireModes?.CurrentAimMode;

                bool CanShootThroughCell(IntVec3 cell)
                {
                    var cover = cell.InBounds(map) ? cell.GetFirstPawn(map) ?? cell.GetCover(map) : null;
                    if (verb.caster.IsOnVehicleMapOf(out var vehicle) && cover == vehicle)
                    {
                        return true;
                    }

                    if (cover != null && cover != verb.ShooterPawn && cover != verb.caster && cover != targetThing && !cover.IsPlant() && !(cover is Pawn && cover.HostileTo(verb.caster)))
                    {
                        //Shooter pawns don't attempt to shoot targets partially obstructed by their own faction members or allies, except when close enough to fire over their shoulder
                        if (cover is Pawn { Downed: false, Faction: not null } cellPawn && verb.ShooterPawn?.Faction != null && (verb.ShooterPawn.Faction == cellPawn.Faction || verb.ShooterPawn.Faction.RelationKindWith(cellPawn.Faction) == FactionRelationKind.Ally) && !cellPawn.AdjacentTo8WayOrInside(verb.ShooterPawn))
                        {
                            return false;
                        }

                        // Skip this check entirely if we're doing suppressive fire and cell is adjacent to target
                        if ((verb.VerbPropsCE.ignorePartialLoSBlocker || aimMode == AimMode.SuppressFire) && cover.def.Fillage != FillCategory.Full)
                        {
                            return true;
                        }

                        var bounds = CE_Utility.GetBoundsFor(cover);

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
                                verb.caster.BaseMap().debugDrawer.FlashCell(cell, 0, bounds.extents.y.ToString(CultureInfo.CurrentCulture));
                            }
                            return false;
                        }

                        if (Controller.settings.DebugDrawPartialLoSChecks)
                        {
                            verb.caster.BaseMap().debugDrawer.FlashCell(cell, 0.7f, bounds.extents.y.ToString(CultureInfo.CurrentCulture));
                        }
                    }

                    return true;
                }

                // Add validator to parameters
                foreach (var curCell in GenSightCE.PointsOnLineOfSight(shotSource, targetPos))
                {
                    if (Controller.settings.DebugDrawPartialLoSChecks)
                    {
                        verb.caster.BaseMap().debugDrawer.FlashCell(curCell, 0.4f);
                    }
                    if (curCell != shotSource.ToIntVec3() && curCell != targetLoc && !CanShootThroughCell(targetThing != null ? curCell.ToThingMapCoord(targetThing) : curCell))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
