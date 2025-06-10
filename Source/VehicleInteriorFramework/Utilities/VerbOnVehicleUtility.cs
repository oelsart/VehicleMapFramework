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
            working = true;
            try
            {
                if (!verb.caster.IsOnVehicleMapOf(out _) && !targ.Thing.IsOnVehicleMapOf(out _) && (!TargetMapManager.HasTargetMap(verb.caster, out var map) || !map.IsVehicleMapOf(out _)))
                {
                    return verb.TryFindShootLineFromTo(root, targ, out resultingLine, ignoreRange);
                }
                if (root == verb.caster.Position)
                {
                    root = verb.caster.PositionOnBaseMap();
                }
                var casterBaseMap = verb.caster.BaseMap();
                var targCellOnBaseMap = TargetMapManager.TargetCellOnBaseMap(ref targ, verb.caster);

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
                    if (verb.CanHitFromCellIgnoringRange(root, targ, out IntVec3 dest))
                    {
                        resultingLine = new ShootLine(root, dest);
                        return true;
                    }
                    ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(verb.caster.Position, occupiedRect.ClosestCellTo(root), verb.caster.Map, VerbOnVehicleUtility.tempLeanShootSources);
                    for (int i = 0; i < VerbOnVehicleUtility.tempLeanShootSources.Count; i++)
                    {
                        IntVec3 intVec = VerbOnVehicleUtility.tempLeanShootSources[i].ToThingBaseMapCoord(verb.caster);
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
                        if (verb.CanHitFromCellIgnoringRange(intVec2, targ, out IntVec3 dest))
                        {
                            resultingLine = new ShootLine(intVec2, dest);
                            return true;
                        }
                    }
                }
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return false;
            }
            finally
            {
                working = false;
            }
        }

        public static bool working;

        public static bool CanHitFromCellIgnoringRange(this Verb verb, IntVec3 sourceCellBaseCol, LocalTargetInfo targ, out IntVec3 goodDest)
        {
            var baseMap = verb.Caster.BaseMap();
            var targCellOnBaseMap = TargetMapManager.TargetCellOnBaseMap(ref targ, verb.caster);
            if (targ.HasThing)
            {
                if (targ.Thing.BaseMap() != baseMap)
                {
                    goodDest = IntVec3.Invalid;
                    return false;
                }
                ShootLeanUtilityOnVehicle.CalcShootableCellsOf(VerbOnVehicleUtility.tempDestList, targ.Thing, sourceCellBaseCol);
                var intVec = sourceCellBaseCol.ToThingMapCoord(targ.Thing);
                for (int i = 0; i < VerbOnVehicleUtility.tempDestList.Count; i++)
                {
                    if (verb.CanHitCellFromCellIgnoringRange(intVec, VerbOnVehicleUtility.tempDestList[i], targ.Thing.Map, targ.Thing.def.Fillage == FillCategory.Full))
                    {
                        goodDest = VerbOnVehicleUtility.tempDestList[i].ToThingBaseMapCoord(targ.Thing);
                        return true;
                    }
                }
            }
            else if (verb.CanHitCellFromCellIgnoringRange(sourceCellBaseCol, targCellOnBaseMap, baseMap, false))
            {
                goodDest = targCellOnBaseMap;
                return true;
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
                    if (!GenSightOnVehicle.LineOfSight(sourceSq, targetLoc, map, false, null))
                    {
                        return false;
                    }
                }
                else if (!GenSightOnVehicle.LineOfSightToEdges(sourceSq, targetLoc, map, false, null))
                {
                    return false;
                }
            }
            return true;
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