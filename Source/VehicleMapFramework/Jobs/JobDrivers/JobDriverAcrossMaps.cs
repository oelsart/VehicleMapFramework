using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public abstract class JobDriverAcrossMaps : JobDriver
{
    protected bool ShouldEnterTargetAMap => exitSpot1.Map != null || enterSpot1.Map != null;

    protected bool ShouldEnterTargetBMap => exitSpot2.Map != null || enterSpot2.Map != null;

    public Map DestMap
    {
        get
        {
            if (destMap != null) return destMap;
            if (enterSpot2.Map != null) return enterSpot2.Map;
            if (exitSpot2.Map != null) return exitSpot2.Map.BaseMap();
            if (enterSpot1.Map != null) return enterSpot1.Map;
            return exitSpot1.Map != null ? exitSpot1.Map.BaseMap() : Map;
        }
    }

    public Map TargetAMap
    {
        get
        {
            if (targetAMap != null) return targetAMap;
            if (enterSpot1.Map != null) return enterSpot1.Map;
            return exitSpot1.Map != null ? exitSpot1.Map.BaseMap() : Map;
        }
    }

    public override Vector3 ForcedBodyOffset => drawOffset;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() =>
            MapNullOrDisposed(exitSpot1) ||
            MapNullOrDisposed(enterSpot1) ||
            MapNullOrDisposed(exitSpot2) ||
            MapNullOrDisposed(enterSpot2));
        yield break;

        static bool MapNullOrDisposed(TargetInfo? spot)
        {
            return spot.HasValue && (spot.Value.Map == null || spot.Value.Map.Disposed);
        }
    }

    public void SetSpots(TargetInfo? exitSpot1_ = null, TargetInfo? enterSpot1_ = null, TargetInfo? exitSpot2_ = null, TargetInfo? enterSpot2_ = null)
    {
        this.exitSpot1 = exitSpot1_ ?? TargetInfo.Invalid;
        this.enterSpot1 = enterSpot1_ ?? TargetInfo.Invalid;
        this.exitSpot2 = exitSpot2_ ?? TargetInfo.Invalid;
        this.enterSpot2 = enterSpot2_ ?? TargetInfo.Invalid;
        targetAMap = TargetAMap;
        destMap = DestMap;

        if (this.exitSpot1 is { IsValid: true, Map: null } ||
            this.enterSpot1 is { IsValid: true, Map: null } ||
            this.exitSpot2 is { IsValid: true, Map: null } ||
            this.enterSpot2 is { IsValid: true, Map: null })
        {
            VMF_Log.Error("SetSpots with null map.");
        }
    }

    protected IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
    {
        switch (ind)
        {
            case TargetIndex.A:
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot1, enterSpot1);
            case TargetIndex.B:
                return ToilsAcrossMaps.GotoTargetMap(this, exitSpot2, enterSpot2);
            case TargetIndex.None:
            case TargetIndex.C:
            default:
                VMF_Log.Error("GotoTargetMap() does not support TargetIndex.C.");
                return null;
        }
    }

    public override void ExposeData()
    {
        Scribe_TargetInfo.Look(ref exitSpot1, "exitSpot1");
        Scribe_TargetInfo.Look(ref enterSpot1, "enterSpot1");
        Scribe_TargetInfo.Look(ref exitSpot2, "exitSpot2");
        Scribe_TargetInfo.Look(ref enterSpot2, "enterSpot2");
        Scribe_Values.Look(ref drawOffset, "drawOffset");
        Scribe_References.Look(ref targetAMap, "targetAMap");
        Scribe_References.Look(ref destMap, "destMap");
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            base.ExposeData();
        });
    }

    private TargetInfo exitSpot1 = TargetInfo.Invalid;

    private TargetInfo enterSpot1 = TargetInfo.Invalid;

    private TargetInfo exitSpot2 = TargetInfo.Invalid;

    private TargetInfo enterSpot2 = TargetInfo.Invalid;

    public Vector3 drawOffset;

    private Map targetAMap;

    private Map destMap;
}