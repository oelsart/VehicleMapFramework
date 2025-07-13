using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public abstract class JobDriverAcrossMaps : JobDriver
{
    public bool ShouldEnterTargetAMap => exitSpot1.Map != null || enterSpot1.Map != null;

    public bool ShouldEnterTargetBMap => exitSpot2.Map != null || enterSpot2.Map != null;

    public Map DestMap
    {
        get
        {
            if (destMap != null) return destMap;
            if (enterSpot2.Map != null) return enterSpot2.Map;
            if (exitSpot2.Map != null) return exitSpot2.Map.BaseMap();
            if (enterSpot1.Map != null) return enterSpot1.Map;
            if (exitSpot1.Map != null) return exitSpot1.Map.BaseMap();
            return base.Map;
        }
    }

    public Map TargetAMap
    {
        get
        {
            if (targetAMap != null) return targetAMap;
            if (enterSpot1.Map != null) return enterSpot1.Map;
            if (exitSpot1.Map != null) return exitSpot1.Map.BaseMap();
            return base.Map;
        }
    }

    public override Vector3 ForcedBodyOffset
    {
        get
        {
            return drawOffset;
        }
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => TargetAMap.Disposed || DestMap.Disposed);
        yield break;
    }

    public void SetSpots(TargetInfo? exitSpot1 = null, TargetInfo? enterSpot1 = null, TargetInfo? exitSpot2 = null, TargetInfo? enterSpot2 = null)
    {
        this.exitSpot1 = exitSpot1 ?? TargetInfo.Invalid;
        this.enterSpot1 = enterSpot1 ?? TargetInfo.Invalid;
        this.exitSpot2 = exitSpot2 ?? TargetInfo.Invalid;
        this.enterSpot2 = enterSpot2 ?? TargetInfo.Invalid;
        targetAMap = TargetAMap;
        destMap = DestMap;

        if ((this.exitSpot1.IsValid && this.exitSpot1.Map == null) ||
            (this.enterSpot1.IsValid && this.enterSpot1.Map == null) ||
            (this.exitSpot2.IsValid && this.exitSpot2.Map == null) ||
            (this.enterSpot2.IsValid && this.enterSpot2.Map == null))
        {
            VMF_Log.Error("SetSpots with null map.");
        }
    }

    public IEnumerable<Toil> GotoTargetMap(TargetIndex ind)
    {
        if (ind == TargetIndex.A)
        {
            return ToilsAcrossMaps.GotoTargetMap(this, exitSpot1, enterSpot1);
        }
        if (ind == TargetIndex.B)
        {
            return ToilsAcrossMaps.GotoTargetMap(this, exitSpot2, enterSpot2);
        }
        VMF_Log.Error("GotoTargetMap() does not support TargetIndex.C.");
        return null;
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