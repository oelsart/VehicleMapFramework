using RimWorld;
using System.Collections.Generic;
using VehicleInteriors.Jobs;
using Vehicles;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;
using Verse.Noise;
using System.Linq;

namespace VehicleInteriors
{
    public static class VerbOnVehicleUtility
    {
        public static bool TryFindShootLineFromToOnVehicle(this Verb verb, IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, bool ignoreRange = false)
        {
            var casterBaseMap = verb.caster.BaseMap();
            var targCellOnBaseMap = targ.CellOnBaseMap();
            if (targ.HasThing && targ.Thing.BaseMap() != casterBaseMap)
            {
                resultingLine = default(ShootLine);
                return false;
            }
            if (verb.verbProps.IsMeleeAttack || verb.EffectiveRange <= 1.42f)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return ReachabilityImmediate.CanReachImmediate(root, targ, verb.caster.Map, PathEndMode.Touch, null);
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
                /*if (verb.CanHitFromCellIgnoringRange(root, targ, out dest))
                {
                    resultingLine = new ShootLine(root, dest);
                    return true;
                }*/
                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(root.ThingMapToOrig(verb.Caster), occupiedRect.ClosestCellTo(root).ThingMapToOrig(verb.Caster), verb.Caster.Map, VerbOnVehicleUtility.tempLeanShootSources);
                for (int i = 0; i < VerbOnVehicleUtility.tempLeanShootSources.Count; i++)
                {
                    if (!VerbOnVehicleUtility.tempLeanShootSources[i].InBounds(verb.Caster.Map)) continue;
                    IntVec3 intVec = VerbOnVehicleUtility.tempLeanShootSources[i].OrigToThingMap(verb.Caster);
                    if (verb.CanHitFromCellIgnoringRange(intVec, targ, out dest) &&
                        GenSightOnVehicle.LineOfSight(VerbOnVehicleUtility.tempLeanShootSources[i], targCellOnBaseMap.ThingMapToOrig(verb.Caster), verb.Caster.Map, true, null))
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
            if (targ.HasThing)
            {
                if (targ.Thing.BaseMap() != verb.caster.BaseMap())
                {
                    goodDest = IntVec3.Invalid;
                    return false;
                }
                return verb.CanHitCellFromCellIgnoringRange(sourceCell, targ, out goodDest);
            }
            else if (verb.CanHitCellFromCellIgnoringRange(sourceCell, targ.Cell, verb.Caster.BaseMap(), false))
            {
                goodDest = targCellOnBaseMap;
                return true;
            }
            goodDest = IntVec3.Invalid;
            return false;
        }

        private static bool CanHitCellFromCellIgnoringRange(this Verb verb, IntVec3 sourceCell, LocalTargetInfo targ, out IntVec3 goodDest)
        {

            var sourceCellOnTargMap = sourceCell.ThingMapToOrig(targ.Thing);
            VerbOnVehicleUtility.CalcShootableCellsOf(VerbOnVehicleUtility.tempDestList, sourceCellOnTargMap, targ.Cell, targ.Thing, targ.Thing.Map);

            for (int i = 0; i < VerbOnVehicleUtility.tempDestList.Count; i++)
            {
                if (verb.CanHitCellFromCellIgnoringRange(sourceCellOnTargMap, VerbOnVehicleUtility.tempDestList[i], targ.Thing.Map, targ.Thing.def.Fillage == FillCategory.Full))
                {
                    goodDest = VerbOnVehicleUtility.tempDestList[i].OrigToThingMap(targ.Thing);
                    return true;
                }
            }
            goodDest = IntVec3.Invalid;
            return false;
        }

        private static bool CanHitCellFromCellIgnoringRange(this Verb verb, IntVec3 sourceSq, IntVec3 targetLoc, Map map, bool includeCorners = false)
        {
            if (verb.verbProps.mustCastOnOpenGround && (!targetLoc.Standable(map) || map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
            {
                return false;
            }
            if (verb.verbProps.requireLineOfSight)
            {
                if (!includeCorners)
                {
                    if (verb.CurrentTarget.HasThing)
                    {
                        if (!GenSightOnVehicle.LineOfSight(sourceSq, targetLoc, map, true, null))
                        {
                            return false;
                        }
                    }
                }
                else if (!GenSightOnVehicle.LineOfSightToEdges(sourceSq, targetLoc, map, true, null))
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
                    foreach (IntVec3 intVec in GenAdj.OccupiedRect(targetPos, target.Rotation, target.def.size))
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

        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();

        private static List<IntVec3> tempDestList = new List<IntVec3>();
    }
}