using System;
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
    
    private List<TraverseSpotsSaveLoader> spotsQueueA_SaveLoader;
    
    private List<TraverseSpotsSaveLoader> spotsQueueB_SaveLoader;
    
    private List<TraverseSpotsSaveLoader> consumedSpots_SaveLoader;

    private Map targetAMap;

    private Map destMap;

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
        if (spots.exitSpot.Map is not null || spots.enterSpot.Map is not null)
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
            TargetIndex.A => ToilsAcrossMaps.GotoTargetMap(this, new TraverseSpots(exitSpotA, enterSpotA)),
            
            TargetIndex.B when !spotsQueueB.NullOrEmpty() =>
                spotsQueueB.SelectMany(s => ToilsAcrossMaps.GotoTargetMap(this, s)),
            TargetIndex.B => ToilsAcrossMaps.GotoTargetMap(this, new TraverseSpots(exitSpotB, enterSpotB)),
            
            _ => new Func<IEnumerable<Toil>>(() =>
            {
                VMF_Log.Error("GotoTargetMap() does not support TargetIndex.C.");
                return [];
            })()
        };
    }

    public override void ExposeData()
    {
        Scribe_TargetInfo.Look(ref exitSpotA, nameof(exitSpotA));
        Scribe_TargetInfo.Look(ref enterSpotA, nameof(enterSpotA));
        Scribe_TargetInfo.Look(ref exitSpotB, nameof(exitSpotB));
        Scribe_TargetInfo.Look(ref enterSpotB, nameof(enterSpotB));
        Scribe_Values.Look(ref drawOffset, nameof(drawOffset));
        Scribe_References.Look(ref targetAMap, nameof(targetAMap));
        Scribe_References.Look(ref destMap, nameof(destMap));

        var flag = Scribe.mode == LoadSaveMode.Saving;
        if (flag)
        {
            spotsQueueA_SaveLoader = spotsQueueA?.Select(spots => new TraverseSpotsSaveLoader(spots)).ToList();
            spotsQueueB_SaveLoader = spotsQueueB?.Select(spots => new TraverseSpotsSaveLoader(spots)).ToList();
            consumedSpots_SaveLoader = consumedSpots?.Select(spots => new TraverseSpotsSaveLoader(spots)).ToList();
        }
        Scribe_Collections.Look(ref spotsQueueA_SaveLoader, nameof(spotsQueueA), LookMode.Deep);
        Scribe_Collections.Look(ref spotsQueueB_SaveLoader, nameof(spotsQueueB), LookMode.Deep);
        Scribe_Collections.Look(ref consumedSpots_SaveLoader, nameof(consumedSpots), LookMode.Deep);
        var flag2 = Scribe.mode == LoadSaveMode.PostLoadInit;
        if (flag2)
        {
            spotsQueueA = spotsQueueA_SaveLoader?.Select(loader => loader.spots).ToList();
            spotsQueueB = spotsQueueB_SaveLoader?.Select(loader => loader.spots).ToList();
            consumedSpots = consumedSpots_SaveLoader?.Select(loader => loader.spots).ToList();
        }

        if (flag || flag2)
        {
            spotsQueueA_SaveLoader = null;
            spotsQueueB_SaveLoader = null;
            consumedSpots_SaveLoader = null;
        }

        base.ExposeData();
    }
}