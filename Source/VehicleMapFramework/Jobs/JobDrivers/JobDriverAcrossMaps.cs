global using TraverseSpots = (Verse.TargetInfo exitSpot, Verse.TargetInfo enterSpot);using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public abstract class JobDriverAcrossMaps : JobDriverBodyOffset
{
    private TargetInfo exitSpotA = TargetInfo.Invalid;

    private TargetInfo enterSpotA = TargetInfo.Invalid;

    private TargetInfo exitSpotB = TargetInfo.Invalid;

    private TargetInfo enterSpotB = TargetInfo.Invalid;
    
    private List<TraverseSpots> spotsQueueA;
    
    private List<TraverseSpots> spotsQueueB;

    private List<TraverseSpots> consumedSpots = [];

    private Map targetAMap;

    private Map destMap;

    protected bool ShouldEnterTargetAMap =>
        !spotsQueueA.NullOrEmpty() && spotsQueueA.Any(s => s.exitSpot.Map != null || s.enterSpot.Map != null) ||
        exitSpotA.Map != null || enterSpotA.Map != null;

    protected bool ShouldEnterTargetBMap =>
        !spotsQueueB.NullOrEmpty() && spotsQueueB.Any(s => s.exitSpot.Map != null || s.enterSpot.Map != null) ||
        exitSpotB.Map != null || enterSpotB.Map != null;

    public Map DestMap
    {
        get
        {
            if (destMap != null) return destMap;
            if (!spotsQueueB.NullOrEmpty())
            {
                var last = spotsQueueB.Last();
                if (last.enterSpot.Map != null) return last.enterSpot.Map;
                if (last.exitSpot.Map != null) return last.exitSpot.Map;
            }
            if (!spotsQueueA.NullOrEmpty())
            {
                var last = spotsQueueA.Last();
                if (last.enterSpot.Map != null) return last.enterSpot.Map;
                if (last.exitSpot.Map != null) return last.exitSpot.Map;
            }
            if (enterSpotB.Map != null) return enterSpotB.Map;
            if (exitSpotB.Map != null) return exitSpotB.Map.BaseMap();
            if (enterSpotA.Map != null) return enterSpotA.Map;
            return exitSpotA.Map != null ? exitSpotA.Map.BaseMap() : Map;
        }
    }

    public Map TargetAMap
    {
        get
        {
            if (targetAMap != null) return targetAMap;
            if (!spotsQueueA.NullOrEmpty())
            {
                var last = spotsQueueA.Last();
                if (last.enterSpot.Map != null) return last.enterSpot.Map;
                if (last.exitSpot.Map != null) return last.exitSpot.Map;
            }
            if (enterSpotA.Map != null) return enterSpotA.Map;
            return exitSpotA.Map != null ? exitSpotA.Map.BaseMap() : Map;
        }
    }

    public override Vector3 ForcedBodyOffset => drawOffset;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() =>
            MapNullOrDisposed(exitSpotA) ||
            MapNullOrDisposed(enterSpotA) ||
            MapNullOrDisposed(exitSpotB) ||
            MapNullOrDisposed(enterSpotB));
        yield break;

        static bool MapNullOrDisposed(TargetInfo spot)
        {
            return spot.IsValid && (spot.Map == null || spot.Map.Disposed);
        }
    }

    public void SetSpots(TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null)
    {
        consumedSpots.Clear();
        exitSpotA = exitSpot1 ?? TargetInfo.Invalid;
        enterSpotA = enterSpot1 ?? TargetInfo.Invalid;
        exitSpotB = exitSpot2 ?? TargetInfo.Invalid;
        enterSpotB = enterSpot2 ?? TargetInfo.Invalid;
        targetAMap = TargetAMap;
        destMap = DestMap;

        if (exitSpotA is { IsValid: true, Map: null } ||
            enterSpotA is { IsValid: true, Map: null } ||
            exitSpotB is { IsValid: true, Map: null } ||
            enterSpotB is { IsValid: true, Map: null })
            VMF_Log.Error("SetSpots with null map.");
    }

    public void SetSpots(List<TraverseSpots> _spotsQueueA = null, List<TraverseSpots> _spotsQueueB = null)
    {
        consumedSpots.Clear();
        spotsQueueA = _spotsQueueA;
        spotsQueueB = _spotsQueueB;
        targetAMap = TargetAMap;
        destMap = DestMap;
        if (spotsQueueA != null && spotsQueueA.Any(MapAnyNull) ||
            spotsQueueB != null && spotsQueueB.Any(MapAnyNull))
            VMF_Log.Error("SetSpots with null map.");
        return;

        static bool MapAnyNull(TraverseSpots spots) =>
            spots.exitSpot is { IsValid: true, Map: null } || spots.enterSpot is { IsValid: true, Map: null };
    }

    public void ConsumeSpots(TraverseSpots spots)
    {
        consumedSpots.Add(spots);
    }

    public bool Consumed(TraverseSpots spots)
    {
        return consumedSpots.Contains(spots);
    }

    protected IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
    {
        return ind switch
        {
            TargetIndex.A when !spotsQueueA.NullOrEmpty() =>
                spotsQueueA.SelectMany(s => ToilsAcrossMaps.GotoTargetMap(this, s)),
            TargetIndex.A => ToilsAcrossMaps.GotoTargetMap(this, (exitSpotA, enterSpotA)),
            
            TargetIndex.B when !spotsQueueB.NullOrEmpty() =>
                spotsQueueB.SelectMany(s => ToilsAcrossMaps.GotoTargetMap(this, s)),
            TargetIndex.B => ToilsAcrossMaps.GotoTargetMap(this, (exitSpotB, enterSpotB)),
            
            _ => new Func<IEnumerable<Toil>>(() =>
            {
                VMF_Log.Error("GotoTargetMap() does not support TargetIndex.C.");
                return [];
            })()
        };
    }

    public override void ExposeData()
    {
        Scribe_TargetInfo.Look(ref exitSpotA, "exitSpot1");
        Scribe_TargetInfo.Look(ref enterSpotA, "enterSpot1");
        Scribe_TargetInfo.Look(ref exitSpotB, "exitSpot2");
        Scribe_TargetInfo.Look(ref enterSpotB, "enterSpot2");
        Scribe_Values.Look(ref drawOffset, "drawOffset");
        Scribe_References.Look(ref targetAMap, "targetAMap");
        Scribe_References.Look(ref destMap, "destMap");
        switch (Scribe.mode)
        {
            case LoadSaveMode.Saving:
            {
                if (spotsQueueA is not null)
                {
                    var tmpExitSpots = spotsQueueA.Select(s => s.exitSpot).ToList();
                    var tmpEnterSpots = spotsQueueA.Select(s => s.enterSpot).ToList();
                    Scribe_Collections.Look(ref tmpExitSpots, "exitSpotsA", LookMode.TargetInfo);
                    Scribe_Collections.Look(ref tmpEnterSpots, "enterSpotsA", LookMode.TargetInfo);
                }
                if (spotsQueueB is not null)
                {
                    var tmpExitSpots = spotsQueueB.Select(s => s.exitSpot).ToList();
                    var tmpEnterSpots = spotsQueueB.Select(s => s.enterSpot).ToList();
                    Scribe_Collections.Look(ref tmpExitSpots, "exitSpotsB", LookMode.TargetInfo);
                    Scribe_Collections.Look(ref tmpEnterSpots, "enterSpotsB", LookMode.TargetInfo);
                }

                if (consumedSpots is not null)
                {
                    var tmpExitSpots = consumedSpots.Select(s => s.exitSpot).ToList();
                    var tmpEnterSpots = consumedSpots.Select(s => s.enterSpot).ToList();
                    Scribe_Collections.Look(ref tmpExitSpots, "consumedExitSpots", LookMode.TargetInfo);
                    Scribe_Collections.Look(ref tmpEnterSpots, "consumedEnterSpots", LookMode.TargetInfo);
                }
                break;
            }
            case LoadSaveMode.LoadingVars:
            {
                List<TargetInfo> tmpExitSpots = null;
                List<TargetInfo> tmpEnterSpots = null;
                Scribe_Collections.Look(ref tmpExitSpots, "exitSpotsA", LookMode.TargetInfo);
                Scribe_Collections.Look(ref tmpEnterSpots, "enterSpotsA", LookMode.TargetInfo);
                if (tmpExitSpots is not null && tmpEnterSpots is not null)
                {
                    spotsQueueA = tmpExitSpots.Zip(tmpEnterSpots, (exitSpot, enterSpot) => (exitSpot, enterSpot)).ToList();
                }
                tmpExitSpots = null;
                tmpEnterSpots = null;
                Scribe_Collections.Look(ref tmpExitSpots, "exitSpotsB", LookMode.TargetInfo);
                Scribe_Collections.Look(ref tmpEnterSpots, "enterSpotsB", LookMode.TargetInfo);
                if (tmpExitSpots is not null && tmpEnterSpots is not null)
                {
                    spotsQueueB = tmpExitSpots.Zip(tmpEnterSpots, (exitSpot, enterSpot) => (exitSpot, enterSpot)).ToList();
                }
                tmpExitSpots = null;
                tmpEnterSpots = null;
                Scribe_Collections.Look(ref tmpExitSpots, "consumedExitSpots", LookMode.TargetInfo);
                Scribe_Collections.Look(ref tmpEnterSpots, "consumedEnterSpots", LookMode.TargetInfo);
                if (tmpExitSpots is not null && tmpEnterSpots is not null)
                {
                    consumedSpots = tmpExitSpots.Zip(tmpEnterSpots, (exitSpot, enterSpot) => (exitSpot, enterSpot)).ToList();
                }
                break;
            }
            case LoadSaveMode.Inactive:
            case LoadSaveMode.ResolvingCrossRefs:
            case LoadSaveMode.PostLoadInit:
            default: break;
        }

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            base.ExposeData();
        });
    }
}