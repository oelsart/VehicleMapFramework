using RimWorld;
using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class VerbOnVehicleUtility
    {
        public static bool TryFindShootLineFromToOnVehicle(this Verb verb, IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, bool ignoreRange = false)
        {
            var casterBaseMap = verb.caster.BaseMap();
            var targCellOnBaseMap = targ.CellOnBaseMap();
            var targMap = targ.HasThing ? targ.Thing.Map : casterBaseMap;
            if (targ.HasThing && targ.Thing.BaseMap() != casterBaseMap)
            {
                resultingLine = default;
                return false;
            }
            if (verb.verbProps.IsMeleeAttack || verb.EffectiveRange <= 1.42f)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return ReachabilityImmediate.CanReachImmediate(verb.caster.Position, targ, verb.caster.Map, PathEndMode.Touch, null);
            }
            CellRect occupiedRect = targ.HasThing ? targ.Thing.MovedOccupiedRect() : CellRect.SingleCell(targCellOnBaseMap);
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
                IntVec3 dest;
                if (verb.CanHitFromCellIgnoringRange(root, targ, out dest))
                {
                    resultingLine = new ShootLine(root, dest);
                    return true;
                }
                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(root, occupiedRect.ClosestCellTo(root), casterBaseMap, VerbOnVehicleUtility.tempLeanShootSources);
                for (int i = 0; i < VerbOnVehicleUtility.tempLeanShootSources.Count; i++)
                {
                    if (!VerbOnVehicleUtility.tempLeanShootSources[i].ThingMapToOrig(verb.Caster).InBounds(verb.Caster.Map)) continue;
                    IntVec3 intVec = VerbOnVehicleUtility.tempLeanShootSources[i];
                    if (verb.CanHitFromCellIgnoringRange(intVec, targ, out dest))
                    {
                        resultingLine = new ShootLine(intVec, dest);
                        return true;
                    }
                }
            }
            else
            {
                foreach (IntVec3 intVec2 in verb.Caster.MovedOccupiedRect())
                {
                    IntVec3 dest;
                    if (verb.CanHitFromCellIgnoringRange(intVec2, targ, out dest))
                    {
                        resultingLine = new ShootLine(intVec2, dest);
                        return true;
                    }
                }
            }
            resultingLine = new ShootLine(root, targCellOnBaseMap);
            return false;
        }

        private static bool CanHitFromCellIgnoringRange(this Verb verb, IntVec3 sourceCell, LocalTargetInfo targ, out IntVec3 goodDest)
        {
            var targCellOnBaseMap = targ.CellOnBaseMap();
            var baseMap = verb.Caster.BaseMap();
            if (targ.HasThing)
            {
                if (targ.Thing.BaseMap() != baseMap)
                {
                    goodDest = IntVec3.Invalid;
                    return false;
                }
                VerbOnVehicleUtility.CalcShootableCellsOf(VerbOnVehicleUtility.tempDestList, sourceCell, targCellOnBaseMap, targ.Thing, baseMap);
                for (int i = 0; i < VerbOnVehicleUtility.tempDestList.Count; i++)
                {
                    if (verb.CanHitCellFromCellIgnoringRange(sourceCell, VerbOnVehicleUtility.tempDestList[i], targ.Thing.Map, targ.Thing.def.Fillage == FillCategory.Full))
                    {
                        goodDest = VerbOnVehicleUtility.tempDestList[i];
                        return true;
                    }
                }
            }
            else if (verb.CanHitCellFromCellIgnoringRange(sourceCell, targ.Cell, baseMap, false))
            {
                goodDest = targCellOnBaseMap;
                return true;
            }
            goodDest = IntVec3.Invalid;
            return false;
        }

        private static bool CanHitCellFromCellIgnoringRange(this Verb verb, IntVec3 sourceSq, IntVec3 targetLoc, Map map, bool includeCorners = false)
        {
            var targetLocOrig = targetLoc;
            var baseMap = map;
            if (map.IsVehicleMapOf(out var vehicle))
            {
                targetLocOrig = targetLoc.VehicleMapToOrig(vehicle);
                baseMap = vehicle.Map;
            }

            if (verb.verbProps.mustCastOnOpenGround && (!targetLocOrig.Standable(map) || map.thingGrid.CellContains(targetLocOrig, ThingCategory.Pawn)))
            {
                return false;
            }
            if (verb.verbProps.requireLineOfSight)
            {
                if (!includeCorners)
                {
                    if (verb.CurrentTarget.HasThing)
                    {
                        if (!GenSightOnVehicle.LineOfSight(sourceSq, targetLoc, baseMap, true, null))
                        {
                            return false;
                        }
                    }
                }
                else if (!GenSightOnVehicle.LineOfSightToEdges(sourceSq, targetLoc, baseMap, true, null))
                {
                    return false;
                }
            }
            return true;
        }

        private static void CalcShootableCellsOf(List<IntVec3> outCells, IntVec3 shooterPos, IntVec3 targetPos, Thing target, Map map)
        {
            outCells.Clear();
            if (target is Pawn)
            {
                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(targetPos, shooterPos, map, outCells);
            }
            else
            {
                outCells.Add(targetPos);
                if (target.def.size.x != 1 || target.def.size.z != 1)
                {
                    foreach (IntVec3 intVec in GenAdj.OccupiedRect(targetPos, target.BaseRotation(), target.def.size))
                    {
                        if (intVec != targetPos)
                        {
                            outCells.Add(intVec);
                        }
                    }
                }
            }
        }

        public static bool CausesTimeSlowdown(this Verb verb, LocalTargetInfo castTarg)
        {
            if (!verb.verbProps.CausesTimeSlowdown)
            {
                return false;
            }
            if (!castTarg.HasThing)
            {
                return false;
            }
            Thing thing = castTarg.Thing;
            if (thing.def.category != ThingCategory.Pawn && (thing.def.building == null || !thing.def.building.IsTurret))
            {
                return false;
            }
            Pawn pawn = thing as Pawn;
            bool flag = pawn != null && pawn.Downed;
            return (verb.CasterPawn == null || verb.CasterPawn.Faction != Faction.OfPlayer || !verb.CasterPawn.IsShambler) && (pawn == null || pawn.Faction != Faction.OfPlayer || !pawn.IsShambler) && ((thing.Faction == Faction.OfPlayer && verb.caster.HostileTo(Faction.OfPlayer)) || (verb.caster.Faction == Faction.OfPlayer && thing.HostileTo(Faction.OfPlayer) && !flag));
        }

        private static readonly List<Thing> cellThingsFiltered = new List<Thing>();

        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();

        private static List<IntVec3> tempDestList = new List<IntVec3>();
    }
}