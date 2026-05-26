using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_DeconstructSegment : JobDriver_RemoveBuilding
{
    protected override DesignationDef Designation => VMF_DefOf.VMF_RemoveSegment;

    protected override EffecterDef WorkEffecter => null;

    protected override float TotalNeededWork => Mathf.Clamp(Building.GetStatValue(StatDefOf.WorkToBuild), 20f, 3000f);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => Building is null || !Building.TryGetComp<CompMapExpander>(out var comp) || comp.IsOnlyBridge);
        foreach (var toil in base.MakeNewToils())
        {
            yield return toil;
            if (toil.debugName == "GotoThing")
            {
                var toil2 = ToilMaker.MakeToil();
                toil2.initAction = () =>
                {
                    if (!RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn, Target, out var cell))
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    }
                    pawn.pather.StartPath(cell, PathEndMode.OnCell);
                };
                toil2.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                yield return toil2;
            }
        }
    }

    protected override void FinishedRemoving()
    {
        Thing.allowDestroyNonDestroyable = true;
        Target.Destroy(DestroyMode.Deconstruct);
        Thing.allowDestroyNonDestroyable = false;
        pawn.records.Increment(RecordDefOf.ThingsDeconstructed);
    }
    
    protected override void TickActionInterval(int delta)
    {
        if (pawn.skills != null && Building.def.CostListAdjusted(Building.Stuff).Count > 0)
        {
            pawn.skills.Learn(SkillDefOf.Construction, 0.25f * delta);
        }
    }
}