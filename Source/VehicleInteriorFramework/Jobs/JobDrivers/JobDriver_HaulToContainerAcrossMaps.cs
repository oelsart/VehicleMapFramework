using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors;

public class JobDriver_HaulToContainerAcrossMaps : JobDriverAcrossMaps
{
    public Thing ThingToCarry
    {
        get
        {
            return (Thing)job.GetTarget(TargetIndex.A);
        }
    }

    public Thing Container
    {
        get
        {
            return (Thing)job.GetTarget(TargetIndex.B);
        }
    }

    public ThingDef ThingDef
    {
        get
        {
            return ThingToCarry.def;
        }
    }

    protected virtual int Duration
    {
        get
        {
            if (Container == null || Container is not Building building)
            {
                return 0;
            }
            return building.HaulToContainerDuration(ThingToCarry);
        }
    }

    protected virtual EffecterDef WorkEffecter
    {
        get
        {
            return null;
        }
    }

    protected virtual SoundDef WorkSustainer
    {
        get
        {
            return null;
        }
    }

    public override string GetReport()
    {
        Thing thing;
        if (pawn.CurJob == job && pawn.carryTracker.CarriedThing != null)
        {
            thing = pawn.carryTracker.CarriedThing;
        }
        else
        {
            thing = base.TargetThingA;
        }
        if (thing == null || !job.targetB.HasThing)
        {
            return "ReportHaulingUnknown".Translate();
        }
        return ((job.GetTarget(TargetIndex.B).Thing is Building_Grave) ? "ReportHaulingToGrave" : "ReportHaulingTo").Translate(thing.Label, job.targetB.Thing.LabelShort.Named("DESTINATION"), thing.Named("THING"));
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!pawn.Reserve(ThingToCarry.MapHeld, job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed, false))
        {
            return false;
        }
        if (Container.Isnt<IHaulEnroute>())
        {
            if (!pawn.Reserve(Container.MapHeld, job.GetTarget(TargetIndex.B), job, 1, 1, null, errorOnFailed, false))
            {
                return false;
            }
            pawn.ReserveAsManyAsPossible(Container.MapHeld, job.GetTargetQueue(TargetIndex.B), job, 1, -1, null);
        }
        UpdateEnrouteTrackers();
        pawn.ReserveAsManyAsPossible(ThingToCarry.MapHeld, job.GetTargetQueue(TargetIndex.A), job, 1, -1, null);
        return true;
    }

    protected virtual void ModifyPrepareToil(Toil toil)
    {
    }

    private bool TryReplaceWithFrame(TargetIndex index)
    {
        Thing thing = base.GetActor().jobs.curJob.GetTarget(index).Thing;
        Building edifice = thing.Position.GetEdifice(pawn.Map);
        if (edifice != null && thing is Blueprint_Build blueprint_Build && edifice is Frame frame && frame.BuildDef == blueprint_Build.BuildDef)
        {
            job.SetTarget(TargetIndex.B, frame);
            return true;
        }
        return false;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOn(delegate ()
        {
            Thing thing = base.GetActor().jobs.curJob.GetTarget(TargetIndex.B).Thing;
            Thing thing2 = base.GetActor().jobs.curJob.GetTarget(TargetIndex.C).Thing;
            if (thing == null)
            {
                return true;
            }
            if (thing2 != null && thing2.Destroyed && !TryReplaceWithFrame(TargetIndex.C))
            {
                job.SetTarget(TargetIndex.C, null);
            }
            if (!thing.Spawned || (thing.Destroyed && !TryReplaceWithFrame(TargetIndex.B)))
            {
                if (job.targetQueueB.NullOrEmpty<LocalTargetInfo>())
                {
                    return true;
                }
                if (!ToilsAcrossMaps.TryGetNextDestinationFromQueue(TargetIndex.C, TargetIndex.B, ThingDef, job, pawn, out Thing nextTarget))
                {
                    return true;
                }
                job.targetQueueB.RemoveAll(target => target.Thing == nextTarget);
                job.targetB = nextTarget;
            }
            ThingOwner thingOwner = Container.TryGetInnerInteractableThingOwner();
            IHaulDestination haulDestination;
            return (thingOwner != null && !thingOwner.CanAcceptAnyOf(ThingToCarry, true)) || ((haulDestination = Container as IHaulDestination) != null && !haulDestination.Accepts(ThingToCarry));
        });
        this.FailOnForbidden(TargetIndex.B);
        this.FailOn(() => EnterPortalUtility.WasLoadingCanceled(Container));
        this.FailOn(() => TransporterUtility.WasLoadingCanceled(Container));
        this.FailOn(() => CompBiosculpterPod.WasLoadingCanceled(Container));
        this.FailOn(() => Building_SubcoreScanner.WasLoadingCancelled(Container));
        Toil getToHaulTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, true);
        Toil uninstallIfMinifiable = Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A).FailOnDestroyedOrNull(TargetIndex.A);
        Toil startCarryingThing = Toils_Haul.StartCarryThing(TargetIndex.A, false, true, false, true, true);
        Toil jumpIfAlsoCollectingNextTarget = Toils_Haul.JumpIfAlsoCollectingNextTargetInQueue(getToHaulTarget, TargetIndex.A);
        Toil carryToContainer = Toils_Haul.CarryHauledThingToContainer();
        yield return Toils_Jump.JumpIf(jumpIfAlsoCollectingNextTarget, () => pawn.IsCarryingThing(ThingToCarry));
        if (ShouldEnterTargetAMap)
        {
            foreach (var toil2 in GotoTargetMap(CarryThingIndex)) yield return toil2;
        }
        yield return getToHaulTarget;
        yield return uninstallIfMinifiable;
        yield return startCarryingThing;
        yield return jumpIfAlsoCollectingNextTarget;
        if (ShouldEnterTargetBMap)
        {
            foreach (var toil2 in GotoTargetMap(DestIndex)) yield return toil2;
        }
        yield return carryToContainer;
        yield return Toils_Goto.MoveOffTargetBlueprint(TargetIndex.B);
        Toil toil = Toils_General.Wait(Duration, TargetIndex.B);
        toil.WithProgressBarToilDelay(TargetIndex.B, false, -0.5f);
        EffecterDef workEffecter = WorkEffecter;
        if (workEffecter != null)
        {
            toil.WithEffect(workEffecter, TargetIndex.B, null);
        }
        SoundDef workSustainer = WorkSustainer;
        if (workSustainer != null)
        {
            toil.PlaySustainerOrSound(workSustainer, 1f);
        }
        Thing destThing = job.GetTarget(TargetIndex.B).Thing;
        toil.tickAction = delegate ()
        {
            if (pawn.IsHashIntervalTick(80) && destThing is Building_Grave && graveDigEffect == null)
            {
                graveDigEffect = EffecterDefOf.BuryPawn.Spawn();
                graveDigEffect.Trigger(destThing, destThing, -1);
            }
            Effecter effecter = graveDigEffect;
            if (effecter == null)
            {
                return;
            }
            effecter.EffectTick(destThing, destThing);
        };
        ModifyPrepareToil(toil);
        yield return toil;
        yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B, TargetIndex.C);
        yield return ToilsAcrossMaps.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.C, null);
        yield return Toils_Haul.JumpToCarryToNextContainerIfPossible(carryToContainer, TargetIndex.C);
        yield break;
    }

    private void UpdateEnrouteTrackers()
    {
        int count = job.count;
        TryReserveEnroute(base.TargetThingC, ref count);
        if (base.TargetB != base.TargetC)
        {
            TryReserveEnroute(base.TargetThingB, ref count);
        }
        if (job.targetQueueB != null)
        {
            foreach (LocalTargetInfo a in job.targetQueueB)
            {
                if (!base.TargetC.HasThing || !(a == base.TargetThingC))
                {
                    TryReserveEnroute(a.Thing, ref count);
                }
            }
        }
    }

    private void TryReserveEnroute(Thing thing, ref int count)
    {
        if (thing is IHaulEnroute container && !thing.DestroyedOrNull())
        {
            UpdateTracker(container, ref count);
        }
    }

    private void UpdateTracker(IHaulEnroute container, ref int count)
    {
        if (ThingToCarry.DestroyedOrNull())
        {
            return;
        }
        if (job.playerForced && container.GetSpaceRemainingWithEnroute(ThingDef, null) == 0)
        {
            container.Map.enrouteManager.InterruptEnroutePawns(container, pawn);
        }
        int num = Mathf.Min(count, container.GetSpaceRemainingWithEnroute(ThingDef, null));
        if (num > 0)
        {
            container.Map.enrouteManager.AddEnroute(container, pawn, base.TargetThingA.def, num);
        }
        count -= num;
    }

    private Effecter graveDigEffect;

    protected const TargetIndex CarryThingIndex = TargetIndex.A;

    public const TargetIndex DestIndex = TargetIndex.B;

    protected const TargetIndex PrimaryDestIndex = TargetIndex.C;

    protected const int DiggingEffectInterval = 80;
}
