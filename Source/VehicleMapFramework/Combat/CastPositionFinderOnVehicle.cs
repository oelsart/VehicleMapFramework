using LudeonTK;
using RimWorld;
using Unity.Collections;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class CastPositionFinderOnVehicle
{
    public static bool TryFindCastPosition(Verse.AI.CastPositionRequest newReq, out IntVec3 dest)
    {
        req = newReq;
        casterLoc = req.caster.Position;
        targetLoc = req.target.PositionOnAnotherThingMap(req.caster);
        verb = req.verb;
        CastPositionFinderOnVehicle.avoidGrid = newReq.caster.TryGetAvoidGrid(out var avoidGrid) ?
            avoidGrid.Grid : emptyByteArray.AsReadOnly();

        var casterPositionOnBaseMap = req.caster.PositionOnBaseMap();

        if (verb == null)
        {
            Log.Error(req.caster + " tried to find casting position without a verb.");
            dest = IntVec3.Invalid;
            return false;
        }
        if (req.maxRegions > 0)
        {
            Region region = casterLoc.GetRegion(req.caster.Map, RegionType.Set_Passable);
            if (region == null)
            {
                Log.Error("TryFindCastPosition requiring region traversal but root region is null.");
                dest = IntVec3.Invalid;
                return false;
            }
            inRadiusMark = Rand.Int;
            RegionTraverser.MarkRegionsBFS(region, null, newReq.maxRegions, inRadiusMark, RegionType.Set_Passable);
            if (req.maxRangeFromLocus > 0.01f)
            {
                Region locusReg = req.locus.GetRegion(req.caster.Map, RegionType.Set_Passable);
                if (locusReg == null)
                {
                    Log.Error("locus " + req.locus + " has no region");
                    dest = IntVec3.Invalid;
                    return false;
                }
                if (locusReg.mark != inRadiusMark)
                {
                    inRadiusMark = Rand.Int;
                    RegionTraverser.BreadthFirstTraverse(region, null, delegate (Region r)
                    {
                        r.mark = inRadiusMark;
                        req.maxRegions++;
                        return r == locusReg;
                    }, 999999, RegionType.Set_Passable);
                }
            }
        }
        CellRect cellRect = CellRect.WholeMap(req.caster.Map);
        if (req.maxRangeFromCaster > 0.01f)
        {
            int num = Mathf.CeilToInt(req.maxRangeFromCaster);
            CellRect otherRect = new(casterLoc.x - num, casterLoc.z - num, (num * 2) + 1, (num * 2) + 1);
            cellRect.ClipInsideRect(otherRect);
        }
        int num2 = Mathf.CeilToInt(req.maxRangeFromTarget);
        CellRect otherRect2 = new(targetLoc.x - num2, targetLoc.z - num2, (num2 * 2) + 1, (num2 * 2) + 1);
        cellRect.ClipInsideRect(otherRect2);
        if (req.maxRangeFromLocus > 0.01f)
        {
            int num3 = Mathf.CeilToInt(req.maxRangeFromLocus);
            CellRect otherRect3 = new(targetLoc.x - num3, targetLoc.z - num3, (num3 * 2) + 1, (num3 * 2) + 1);
            cellRect.ClipInsideRect(otherRect3);
        }
        bestSpot = IntVec3.Invalid;
        bestSpotPref = 0.001f;
        maxRangeFromCasterSquared = req.maxRangeFromCaster * req.maxRangeFromCaster;
        maxRangeFromTargetSquared = req.maxRangeFromTarget * req.maxRangeFromTarget;
        maxRangeFromLocusSquared = req.maxRangeFromLocus * req.maxRangeFromLocus;
        rangeFromTarget = (casterLoc - targetLoc).LengthHorizontal;
        rangeFromTargetSquared = (casterLoc - targetLoc).LengthHorizontalSquared;
        optimalRangeSquared = verb.verbProps.range * 0.8f * (verb.verbProps.range * 0.8f);
        if (req.preferredCastPosition != null && req.preferredCastPosition.Value.IsValid)
        {
            EvaluateCell(req.preferredCastPosition.Value);
            if (bestSpot.IsValid && bestSpotPref > 0.001f)
            {
                dest = req.preferredCastPosition.Value;
                return true;
            }
        }
        EvaluateCell(casterLoc);
        if (bestSpotPref >= 1.0)
        {
            dest = casterLoc;
            return true;
        }
        float slope = -1f / CellLine.Between(targetLoc, casterLoc).Slope;
        CellLine cellLine = new(targetLoc, slope);
        bool flag = cellLine.CellIsAbove(casterLoc);
        foreach (IntVec3 c in cellRect)
        {
            if (cellLine.CellIsAbove(c) == flag && cellRect.Contains(c))
            {
                EvaluateCell(c);
            }
        }
        if (bestSpot.IsValid && bestSpotPref > 0.33f)
        {
            dest = bestSpot;
            return true;
        }
        foreach (IntVec3 c2 in cellRect)
        {
            if (cellLine.CellIsAbove(c2) != flag && cellRect.Contains(c2))
            {
                EvaluateCell(c2);
            }
        }
        if (bestSpot.IsValid)
        {
            dest = bestSpot;
            return true;
        }
        dest = casterLoc;
        return false;
    }

    private static void EvaluateCell(IntVec3 c)
    {
        var casterMap = req.caster.Map;

        if (req.validator != null && !req.validator(c))
        {
            return;
        }
        if (maxRangeFromTargetSquared > 0.01f && maxRangeFromTargetSquared < 250000f && (c - targetLoc).LengthHorizontalSquared > maxRangeFromTargetSquared)
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, 0f, "range target", 50);
            }
            return;
        }
        if ((double)maxRangeFromLocusSquared > 0.01f && (c - req.locus).LengthHorizontalSquared > maxRangeFromLocusSquared)
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, 0.1f, "range home", 50);
            }
            return;
        }
        if (maxRangeFromCasterSquared > 0.01f)
        {
            rangeFromCasterToCellSquared = (c - casterLoc).LengthHorizontalSquared;
            if (rangeFromCasterToCellSquared > maxRangeFromCasterSquared)
            {
                if (DebugViewSettings.drawCastPositionSearch)
                {
                    casterMap.debugDrawer.FlashCell(c, 0.2f, "range caster", 50);
                }
                return;
            }
        }
        if (!c.Standable(casterMap))
        {
            return;
        }
        if (req.maxRegions > 0 && c.GetRegion(casterMap, RegionType.Set_Passable).mark != inRadiusMark)
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, 0.64f, "reg radius", 50);
            }
            return;
        }
        if (!CrossMapReachabilityUtility.CanReach(casterMap, req.caster.Position, c, PathEndMode.OnCell, TraverseParms.For(req.caster, Danger.Some, TraverseMode.ByPawn, false, false, false), req.target.Map, out _, out _) &&
            !req.caster.IsOnVehicleMapOf(out _) && !req.target.IsOnVehicleMapOf(out _))
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, 0.4f, "can't reach", 50);
            }
            return;
        }
        float num = CastPositionPreference(c);
        if (avoidGrid.Length > 0)
        {
            byte b = avoidGrid[req.caster.Map.cellIndices.CellToIndex(c)];
            num *= Mathf.Max(0.1f, (37.5f - b) / 37.5f);
        }
        if (DebugViewSettings.drawCastPositionSearch)
        {
            casterMap.debugDrawer.FlashCell(c, num / 4f, num.ToString("F3"), 50);
        }
        if (num < bestSpotPref)
        {
            return;
        }
        IntVec3 cellOnBaseMap = c.ToThingBaseMapCoord(req.caster);
        if (!verb.CanHitTargetFrom(cellOnBaseMap, req.target))
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, 0.6f, "can't hit", 50);
            }
            return;
        }
        if (!casterMap.pawnDestinationReservationManager.CanReserve(c, req.caster, false))
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, num * 0.9f, "resvd", 50);
            }
            return;
        }
        if (PawnUtility.KnownDangerAt(c, casterMap, req.caster))
        {
            if (DebugViewSettings.drawCastPositionSearch)
            {
                casterMap.debugDrawer.FlashCell(c, 0.9f, "danger", 50);
            }
            return;
        }
        bestSpot = c;
        bestSpotPref = num;
    }

    private static float CastPositionPreference(IntVec3 c)
    {
        bool flag = true;
        foreach (var thing in req.caster.Map.thingGrid.ThingsAt(c))
        {
            if (thing is Fire fire && fire.parent == null)
            {
                return -1f;
            }
            if (thing.def.passability == Traversability.PassThroughOnly)
            {
                flag = false;
            }
        }
        float num = 0.3f;
        if (req.caster.kindDef.aiAvoidCover)
        {
            num += 8f - CoverUtility.TotalSurroundingCoverScore(c, req.caster.Map);
        }
        if (req.wantCoverFromTarget)
        {
            num += CoverUtility.CalculateOverallBlockChance(c, targetLoc, req.caster.Map) * 0.55f;
        }
        float num2 = (casterLoc - c).LengthHorizontal;
        if (rangeFromTarget > 100f)
        {
            num2 -= rangeFromTarget - 100f;
            if (num2 < 0f)
            {
                num2 = 0f;
            }
        }
        num *= Mathf.Pow(0.967f, num2);
        float num3 = 1f;
        rangeFromTargetToCellSquared = (c - targetLoc).LengthHorizontalSquared;
        float num4 = Mathf.Abs(rangeFromTargetToCellSquared - optimalRangeSquared) / optimalRangeSquared;
        num4 = 1f - num4;
        num4 = 0.7f + (0.3f * num4);
        num3 *= num4;
        if (rangeFromTargetToCellSquared < 25f)
        {
            num3 *= 0.5f;
        }
        num *= num3;
        if (rangeFromCasterToCellSquared > rangeFromTargetSquared)
        {
            num *= 0.4f;
        }
        if (!flag)
        {
            num *= 0.2f;
        }
        return num;
    }

    private static Verse.AI.CastPositionRequest req;

    private static IntVec3 casterLoc;

    private static IntVec3 targetLoc;

    private static Verb verb;

    private static float rangeFromTarget;

    private static float rangeFromTargetSquared;

    private static float optimalRangeSquared;

    private static float rangeFromCasterToCellSquared;

    private static float rangeFromTargetToCellSquared;

    private static int inRadiusMark;

    private static NativeArray<byte>.ReadOnly avoidGrid;

    private static float maxRangeFromCasterSquared;

    private static float maxRangeFromTargetSquared;

    private static float maxRangeFromLocusSquared;

    private static IntVec3 bestSpot = IntVec3.Invalid;

    private static float bestSpotPref = 0.001f;

    private static NativeArray<byte> emptyByteArray = NativeArrayUtility.EmptyArray<byte>();

    private const float BaseAIPreference = 0.3f;

    private const float MinimumPreferredRange = 5f;

    private const float OptimalRangeFactor = 0.8f;

    private const float OptimalRangeFactorImportance = 0.3f;

    private const float CoverPreferenceFactor = 0.55f;
}
