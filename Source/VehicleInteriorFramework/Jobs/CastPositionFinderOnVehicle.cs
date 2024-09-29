using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;
using Verse;
using Vehicles;

namespace VehicleInteriors
{
    public static class CastPositionFinderOnVehicle
    {
        public static bool TryFindCastPosition(CastPositionRequest newReq, out IntVec3 dest)
        {
            CastPositionFinderOnVehicle.req = newReq;
            var casterPositionOnBaseMap = CastPositionFinderOnVehicle.req.caster.PositionOnBaseMap();
            IntVec3 targetPositionOnCasterMap;
            if (CastPositionFinderOnVehicle.req.caster.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                targetPositionOnCasterMap = IntVec3.Zero.OrigToVehicleMap(parentVehicle.vehicle) - casterPositionOnBaseMap;
            }
            else
            {
                targetPositionOnCasterMap = CastPositionFinderOnVehicle.req.target.PositionOnBaseMap();
            }

            CastPositionFinderOnVehicle.casterLoc = CastPositionFinderOnVehicle.req.caster.Position;
            CastPositionFinderOnVehicle.targetLoc = targetPositionOnCasterMap;
            CastPositionFinderOnVehicle.verb = CastPositionFinderOnVehicle.req.verb;
            CastPositionFinderOnVehicle.avoidGrid = newReq.caster.GetAvoidGrid(false);

            if (CastPositionFinderOnVehicle.verb == null)
            {
                Log.Error(CastPositionFinderOnVehicle.req.caster + " tried to find casting position without a verb.");
                dest = IntVec3.Invalid;
                return false;
            }
            if (CastPositionFinderOnVehicle.req.maxRegions > 0)
            {
                Region region = CastPositionFinderOnVehicle.casterLoc.GetRegion(CastPositionFinderOnVehicle.req.caster.Map, RegionType.Set_Passable);
                if (region == null)
                {
                    Log.Error("TryFindCastPosition requiring region traversal but root region is null.");
                    dest = IntVec3.Invalid;
                    return false;
                }
                CastPositionFinderOnVehicle.inRadiusMark = Rand.Int;
                RegionTraverser.MarkRegionsBFS(region, null, newReq.maxRegions, CastPositionFinderOnVehicle.inRadiusMark, RegionType.Set_Passable);
                if (CastPositionFinderOnVehicle.req.maxRangeFromLocus > 0.01f)
                {
                    Region locusReg = CastPositionFinderOnVehicle.req.locus.GetRegion(CastPositionFinderOnVehicle.req.caster.Map, RegionType.Set_Passable);
                    if (locusReg == null)
                    {
                        Log.Error("locus " + CastPositionFinderOnVehicle.req.locus + " has no region");
                        dest = IntVec3.Invalid;
                        return false;
                    }
                    if (locusReg.mark != CastPositionFinderOnVehicle.inRadiusMark)
                    {
                        CastPositionFinderOnVehicle.inRadiusMark = Rand.Int;
                        RegionTraverser.BreadthFirstTraverse(region, null, delegate (Region r)
                        {
                            r.mark = CastPositionFinderOnVehicle.inRadiusMark;
                            CastPositionFinderOnVehicle.req.maxRegions = CastPositionFinderOnVehicle.req.maxRegions + 1;
                            return r == locusReg;
                        }, 999999, RegionType.Set_Passable);
                    }
                }
            }
            CellRect cellRect = CellRect.WholeMap(CastPositionFinderOnVehicle.req.caster.Map);
            if (CastPositionFinderOnVehicle.req.maxRangeFromCaster > 0.01f)
            {
                int num = Mathf.CeilToInt(CastPositionFinderOnVehicle.req.maxRangeFromCaster);
                CellRect otherRect = new CellRect(CastPositionFinderOnVehicle.casterLoc.x - num, CastPositionFinderOnVehicle.casterLoc.z - num, num * 2 + 1, num * 2 + 1);
                cellRect.ClipInsideRect(otherRect);
            }
            int num2 = Mathf.CeilToInt(CastPositionFinderOnVehicle.req.maxRangeFromTarget);
            CellRect otherRect2 = new CellRect(CastPositionFinderOnVehicle.targetLoc.x - num2, CastPositionFinderOnVehicle.targetLoc.z - num2, num2 * 2 + 1, num2 * 2 + 1);
            cellRect.ClipInsideRect(otherRect2);
            if (CastPositionFinderOnVehicle.req.maxRangeFromLocus > 0.01f)
            {
                int num3 = Mathf.CeilToInt(CastPositionFinderOnVehicle.req.maxRangeFromLocus);
                CellRect otherRect3 = new CellRect(CastPositionFinderOnVehicle.targetLoc.x - num3, CastPositionFinderOnVehicle.targetLoc.z - num3, num3 * 2 + 1, num3 * 2 + 1);
                cellRect.ClipInsideRect(otherRect3);
            }
            CastPositionFinderOnVehicle.bestSpot = IntVec3.Invalid;
            CastPositionFinderOnVehicle.bestSpotPref = 0.001f;
            CastPositionFinderOnVehicle.maxRangeFromCasterSquared = CastPositionFinderOnVehicle.req.maxRangeFromCaster * CastPositionFinderOnVehicle.req.maxRangeFromCaster;
            CastPositionFinderOnVehicle.maxRangeFromTargetSquared = CastPositionFinderOnVehicle.req.maxRangeFromTarget * CastPositionFinderOnVehicle.req.maxRangeFromTarget;
            CastPositionFinderOnVehicle.maxRangeFromLocusSquared = CastPositionFinderOnVehicle.req.maxRangeFromLocus * CastPositionFinderOnVehicle.req.maxRangeFromLocus;
            CastPositionFinderOnVehicle.rangeFromTarget = (CastPositionFinderOnVehicle.casterLoc - CastPositionFinderOnVehicle.targetLoc).LengthHorizontal;
            CastPositionFinderOnVehicle.rangeFromTargetSquared = (float)(CastPositionFinderOnVehicle.casterLoc - CastPositionFinderOnVehicle.targetLoc).LengthHorizontalSquared;
            CastPositionFinderOnVehicle.optimalRangeSquared = CastPositionFinderOnVehicle.verb.verbProps.range * 0.8f * (CastPositionFinderOnVehicle.verb.verbProps.range * 0.8f);
            if (CastPositionFinderOnVehicle.req.preferredCastPosition != null && CastPositionFinderOnVehicle.req.preferredCastPosition.Value.IsValid)
            {
                CastPositionFinderOnVehicle.EvaluateCell(CastPositionFinderOnVehicle.req.preferredCastPosition.Value);
                if (CastPositionFinderOnVehicle.bestSpot.IsValid && CastPositionFinderOnVehicle.bestSpotPref > 0.001f)
                {
                    dest = CastPositionFinderOnVehicle.req.preferredCastPosition.Value;
                    return true;
                }
            }
            CastPositionFinderOnVehicle.EvaluateCell(CastPositionFinderOnVehicle.casterLoc);
            if ((double)CastPositionFinderOnVehicle.bestSpotPref >= 1.0)
            {
                dest = CastPositionFinderOnVehicle.casterLoc;
                return true;
            }
            float slope = -1f / CellLine.Between(CastPositionFinderOnVehicle.targetLoc, CastPositionFinderOnVehicle.casterLoc).Slope;
            CellLine cellLine = new CellLine(CastPositionFinderOnVehicle.targetLoc, slope);
            bool flag = cellLine.CellIsAbove(CastPositionFinderOnVehicle.casterLoc);
            foreach (IntVec3 c in cellRect)
            {
                if (cellLine.CellIsAbove(c) == flag && cellRect.Contains(c))
                {
                    CastPositionFinderOnVehicle.EvaluateCell(c);
                }
            }
            if (CastPositionFinderOnVehicle.bestSpot.IsValid && CastPositionFinderOnVehicle.bestSpotPref > 0.33f)
            {
                dest = CastPositionFinderOnVehicle.bestSpot;
                return true;
            }
            foreach (IntVec3 c2 in cellRect)
            {
                if (cellLine.CellIsAbove(c2) != flag && cellRect.Contains(c2))
                {
                    CastPositionFinderOnVehicle.EvaluateCell(c2);
                }
            }
            if (CastPositionFinderOnVehicle.bestSpot.IsValid)
            {
                dest = CastPositionFinderOnVehicle.bestSpot;
                return true;
            }
            dest = CastPositionFinderOnVehicle.casterLoc;
            return false;
        }

        private static void EvaluateCell(IntVec3 c)
        {
            var casterVehicleMap = CastPositionFinderOnVehicle.req.caster.Map;

            if (CastPositionFinderOnVehicle.req.validator != null && !CastPositionFinderOnVehicle.req.validator(c))
            {
                return;
            }
            if (CastPositionFinderOnVehicle.maxRangeFromTargetSquared > 0.01f && CastPositionFinderOnVehicle.maxRangeFromTargetSquared < 250000f && (float)(c - CastPositionFinderOnVehicle.targetLoc).LengthHorizontalSquared > CastPositionFinderOnVehicle.maxRangeFromTargetSquared)
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, 0f, "range target", 50);
                }
                return;
            }
            if ((double)CastPositionFinderOnVehicle.maxRangeFromLocusSquared > 0.01f && (float)(c - CastPositionFinderOnVehicle.req.locus).LengthHorizontalSquared > CastPositionFinderOnVehicle.maxRangeFromLocusSquared)
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, 0.1f, "range home", 50);
                }
                return;
            }
            if (CastPositionFinderOnVehicle.maxRangeFromCasterSquared > 0.01f)
            {
                CastPositionFinderOnVehicle.rangeFromCasterToCellSquared = (float)(c - CastPositionFinderOnVehicle.casterLoc).LengthHorizontalSquared;
                if (CastPositionFinderOnVehicle.rangeFromCasterToCellSquared > CastPositionFinderOnVehicle.maxRangeFromCasterSquared)
                {
                    if (DebugViewSettings.drawCastPositionSearch)
                    {
                        casterVehicleMap.debugDrawer.FlashCell(c, 0.2f, "range caster", 50);
                    }
                    return;
                }
            }
            if (!c.Standable(casterVehicleMap))
            {
                return;
            }
            if (CastPositionFinderOnVehicle.req.maxRegions > 0 && c.GetRegion(casterVehicleMap, RegionType.Set_Passable).mark != CastPositionFinderOnVehicle.inRadiusMark)
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, 0.64f, "reg radius", 50);
                }
                return;
            }
            if (!casterVehicleMap.reachability.CanReach(CastPositionFinderOnVehicle.req.caster.Position, c, PathEndMode.OnCell, TraverseParms.For(CastPositionFinderOnVehicle.req.caster, Danger.Some, TraverseMode.ByPawn, false, false, false)))
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, 0.4f, "can't reach", 50);
                }
                return;
            }
            float num = CastPositionFinderOnVehicle.CastPositionPreference(c);
            if (CastPositionFinderOnVehicle.avoidGrid != null)
            {
                byte b = CastPositionFinderOnVehicle.avoidGrid[c];
                num *= Mathf.Max(0.1f, (37.5f - (float)b) / 37.5f);
            }
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterVehicleMap.debugDrawer.FlashCell(c, num / 4f, num.ToString("F3"), 50);
            }
            if (num < CastPositionFinderOnVehicle.bestSpotPref)
            {
                return;
            }
            IntVec3 cellOnBaseMap;
            if (CastPositionFinderOnVehicle.req.caster.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                cellOnBaseMap = c.OrigToVehicleMap(parentVehicle.vehicle);
            }
            else
            {
                cellOnBaseMap = c;
            }
            if (!CastPositionFinderOnVehicle.verb.CanHitTargetFrom(cellOnBaseMap, CastPositionFinderOnVehicle.req.target))
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, 0.6f, "can't hit", 50);
                }
                return;
            }
            if (!casterVehicleMap.pawnDestinationReservationManager.CanReserve(c, CastPositionFinderOnVehicle.req.caster, false))
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, num * 0.9f, "resvd", 50);
                }
                return;
            }
            if (PawnUtility.KnownDangerAt(c, casterVehicleMap, CastPositionFinderOnVehicle.req.caster))
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterVehicleMap.debugDrawer.FlashCell(c, 0.9f, "danger", 50);
                }
                return;
            }
            CastPositionFinderOnVehicle.bestSpot = c;
            CastPositionFinderOnVehicle.bestSpotPref = num;
        }

        private static float CastPositionPreference(IntVec3 c)
        {
            bool flag = true;
            foreach(var thing in CastPositionFinderOnVehicle.req.caster.Map.thingGrid.ThingsAt(c))
            {
                Fire fire = thing as Fire;
                if (fire != null && fire.parent == null)
                {
                    return -1f;
                }
                if (thing.def.passability == Traversability.PassThroughOnly)
                {
                    flag = false;
                }
            }
            float num = 0.3f;
            if (CastPositionFinderOnVehicle.req.caster.kindDef.aiAvoidCover)
            {
                num += 8f - CoverUtility.TotalSurroundingCoverScore(c, CastPositionFinderOnVehicle.req.caster.Map);
            }
            if (CastPositionFinderOnVehicle.req.wantCoverFromTarget)
            {
                num += CoverUtility.CalculateOverallBlockChance(c, CastPositionFinderOnVehicle.targetLoc, CastPositionFinderOnVehicle.req.caster.Map) * 0.55f;
            }
            float num2 = (CastPositionFinderOnVehicle.casterLoc - c).LengthHorizontal;
            if (CastPositionFinderOnVehicle.rangeFromTarget > 100f)
            {
                num2 -= CastPositionFinderOnVehicle.rangeFromTarget - 100f;
                if (num2 < 0f)
                {
                    num2 = 0f;
                }
            }
            num *= Mathf.Pow(0.967f, num2);
            float num3 = 1f;
            CastPositionFinderOnVehicle.rangeFromTargetToCellSquared = (float)(c - CastPositionFinderOnVehicle.targetLoc).LengthHorizontalSquared;
            float num4 = Mathf.Abs(CastPositionFinderOnVehicle.rangeFromTargetToCellSquared - CastPositionFinderOnVehicle.optimalRangeSquared) / CastPositionFinderOnVehicle.optimalRangeSquared;
            num4 = 1f - num4;
            num4 = 0.7f + 0.3f * num4;
            num3 *= num4;
            if (CastPositionFinderOnVehicle.rangeFromTargetToCellSquared < 25f)
            {
                num3 *= 0.5f;
            }
            num *= num3;
            if (CastPositionFinderOnVehicle.rangeFromCasterToCellSquared > CastPositionFinderOnVehicle.rangeFromTargetSquared)
            {
                num *= 0.4f;
            }
            if (!flag)
            {
                num *= 0.2f;
            }
            return num;
        }

        private static CastPositionRequest req;

        private static IntVec3 casterLoc;

        private static IntVec3 targetLoc;

        private static Verb verb;

        private static float rangeFromTarget;

        private static float rangeFromTargetSquared;

        private static float optimalRangeSquared;

        private static float rangeFromCasterToCellSquared;

        private static float rangeFromTargetToCellSquared;

        private static int inRadiusMark;

        private static ByteGrid avoidGrid;

        private static float maxRangeFromCasterSquared;

        private static float maxRangeFromTargetSquared;

        private static float maxRangeFromLocusSquared;

        private static IntVec3 bestSpot = IntVec3.Invalid;

        private static float bestSpotPref = 0.001f;

        private const float BaseAIPreference = 0.3f;

        private const float MinimumPreferredRange = 5f;

        private const float OptimalRangeFactor = 0.8f;

        private const float OptimalRangeFactorImportance = 0.3f;

        private const float CoverPreferenceFactor = 0.55f;
    }
}
