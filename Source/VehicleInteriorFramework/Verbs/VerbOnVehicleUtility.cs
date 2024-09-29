using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;

namespace VehicleInteriors
{
    public static class VerbOnVehicleUtility
    {
        public static bool TryFindShootLineFromToOnVehicle(this Verb verb, IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, bool ignoreRange = false)
        {
            var casterBaseMap = verb.caster.BaseMapOfThing();
            var targCellOnBaseMap = targ.CellOnBaseMap();
            if (targ.HasThing && targ.Thing.BaseMapOfThing() != casterBaseMap)
            {
                resultingLine = default(ShootLine);
                return false;
            }
            if (verb.verbProps.IsMeleeAttack || verb.EffectiveRange <= 1.42f)
            {
                resultingLine = new ShootLine(root, targCellOnBaseMap);
                return ReachabilityImmediate.CanReachImmediate(root, targ, verb.caster.Map, PathEndMode.Touch, null);
            }
            CellRect occupiedRect = targ.HasThing ? targ.Thing.MovedOccupiedRect() : CellRect.SingleCell(targ.Cell);
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
                ShootLeanUtility.LeanShootingSourcesFromTo(root, occupiedRect.ClosestCellTo(root), casterBaseMap, VerbOnVehicleUtility.tempLeanShootSources);
                for (int i = 0; i < VerbOnVehicleUtility.tempLeanShootSources.Count; i++)
                {
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
                foreach (IntVec3 intVec2 in verb.caster.MovedOccupiedRect())
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
            if (targ.Thing != null)
            {
                if (targ.Thing.BaseMapOfThing() != verb.caster.BaseMapOfThing())
                {
                    goodDest = IntVec3.Invalid;
                    return false;
                }
                ShootLeanUtility.CalcShootableCellsOf(VerbOnVehicleUtility.tempDestList, targ.Thing, sourceCell);
                for (int i = 0; i < VerbOnVehicleUtility.tempDestList.Count; i++)
                {
                    if (verb.CanHitCellFromCellIgnoringRange(sourceCell, VerbOnVehicleUtility.tempDestList[i], targ.Thing.def.Fillage == FillCategory.Full))
                    {
                        goodDest = VerbOnVehicleUtility.tempDestList[i];
                        return true;
                    }
                }
            }
            else if (verb.CanHitCellFromCellIgnoringRange(sourceCell, targCellOnBaseMap, false))
            {
                goodDest = targCellOnBaseMap;
                return true;
            }
            goodDest = IntVec3.Invalid;
            return false;
        }

        private static bool CanHitCellFromCellIgnoringRange(this Verb verb, IntVec3 sourceSq, IntVec3 targetLoc, bool includeCorners = false)
        {
            if (verb.verbProps.mustCastOnOpenGround && (!targetLoc.Standable(verb.caster.Map) || verb.caster.Map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
            {
                return false;
            }
            var casterBaseMap = verb.caster.BaseMapOfThing();
            if (verb.verbProps.requireLineOfSight)
            {
                if (!includeCorners)
                {
                    if (!GenSight.LineOfSight(sourceSq, targetLoc, casterBaseMap, true, null, 0, 0))
                    {
                        return false;
                    }
                }
                else if (!GenSight.LineOfSightToEdges(sourceSq, targetLoc, casterBaseMap, true, null))
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

        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();

        private static List<IntVec3> tempDestList = new List<IntVec3>();
    }
}
