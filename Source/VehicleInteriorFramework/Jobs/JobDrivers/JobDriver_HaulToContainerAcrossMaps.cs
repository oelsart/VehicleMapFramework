using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_HaulToContainerAcrossMaps : JobDriverAcrossMaps
    {
        public Thing ThingToCarry
        {
            get
            {
                return (Thing)this.job.GetTarget(TargetIndex.A);
            }
        }

        public Thing Container
        {
            get
            {
                return (Thing)this.job.GetTarget(TargetIndex.B);
            }
        }

        public ThingDef ThingDef
        {
            get
            {
                return this.ThingToCarry.def;
            }
        }

        protected virtual int Duration
        {
            get
            {
                Building building;
                if (this.Container == null || (building = (this.Container as Building)) == null)
                {
                    return 0;
                }
                return building.HaulToContainerDuration(this.ThingToCarry);
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
            if (this.pawn.CurJob == this.job && this.pawn.carryTracker.CarriedThing != null)
            {
                thing = this.pawn.carryTracker.CarriedThing;
            }
            else
            {
                thing = base.TargetThingA;
            }
            if (thing == null || !this.job.targetB.HasThing)
            {
                return "ReportHaulingUnknown".Translate();
            }
            return ((this.job.GetTarget(TargetIndex.B).Thing is Building_Grave) ? "ReportHaulingToGrave" : "ReportHaulingTo").Translate(thing.Label, this.job.targetB.Thing.LabelShort.Named("DESTINATION"), thing.Named("THING"));
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!this.pawn.Reserve(this.ThingToCarry.MapHeld, this.job.GetTarget(TargetIndex.A), this.job, 1, -1, null, errorOnFailed, false))
            {
                return false;
            }
            if (this.Container.Isnt<IHaulEnroute>())
            {
                if (!this.pawn.Reserve(this.Container.MapHeld, this.job.GetTarget(TargetIndex.B), this.job, 1, 1, null, errorOnFailed, false))
                {
                    return false;
                }
                this.pawn.ReserveAsManyAsPossible(this.Container.MapHeld, this.job.GetTargetQueue(TargetIndex.B), this.job, 1, -1, null);
            }
            this.UpdateEnrouteTrackers();
            this.pawn.ReserveAsManyAsPossible(this.ThingToCarry.MapHeld, this.job.GetTargetQueue(TargetIndex.A), this.job, 1, -1, null);
            return true;
        }

        protected virtual void ModifyPrepareToil(Toil toil)
        {
        }

        private bool TryReplaceWithFrame(TargetIndex index)
        {
            Thing thing = base.GetActor().jobs.curJob.GetTarget(index).Thing;
            Building edifice = thing.Position.GetEdifice(this.pawn.Map);
            if (edifice != null && thing is Blueprint_Build blueprint_Build && edifice is Frame frame && frame.BuildDef == blueprint_Build.BuildDef)
            {
                this.job.SetTarget(TargetIndex.B, frame);
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
                if (thing2 != null && thing2.Destroyed && !this.TryReplaceWithFrame(TargetIndex.C))
                {
                    this.job.SetTarget(TargetIndex.C, null);
                }
                if (!thing.Spawned || (thing.Destroyed && !this.TryReplaceWithFrame(TargetIndex.B)))
                {
                    if (this.job.targetQueueB.NullOrEmpty<LocalTargetInfo>())
                    {
                        return true;
                    }
                    if (!ToilsAcrossMaps.TryGetNextDestinationFromQueue(TargetIndex.C, TargetIndex.B, this.ThingDef, this.job, this.pawn, out Thing nextTarget))
                    {
                        return true;
                    }
                    this.job.targetQueueB.RemoveAll((LocalTargetInfo target) => target.Thing == nextTarget);
                    this.job.targetB = nextTarget;
                }
                ThingOwner thingOwner = this.Container.TryGetInnerInteractableThingOwner();
                IHaulDestination haulDestination;
                return (thingOwner != null && !thingOwner.CanAcceptAnyOf(this.ThingToCarry, true)) || ((haulDestination = (this.Container as IHaulDestination)) != null && !haulDestination.Accepts(this.ThingToCarry));
            });
            this.FailOnForbidden(TargetIndex.B);
            this.FailOn(() => EnterPortalUtility.WasLoadingCanceled(this.Container));
            this.FailOn(() => TransporterUtility.WasLoadingCanceled(this.Container));
            this.FailOn(() => CompBiosculpterPod.WasLoadingCanceled(this.Container));
            this.FailOn(() => Building_SubcoreScanner.WasLoadingCancelled(this.Container));
            Toil getToHaulTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, true);
            Toil uninstallIfMinifiable = Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A).FailOnDestroyedOrNull(TargetIndex.A);
            Toil startCarryingThing = Toils_Haul.StartCarryThing(TargetIndex.A, false, true, false, true, true);
            Toil jumpIfAlsoCollectingNextTarget = Toils_Haul.JumpIfAlsoCollectingNextTargetInQueue(getToHaulTarget, TargetIndex.A);
            Toil carryToContainer = Toils_Haul.CarryHauledThingToContainer();
            yield return Toils_Jump.JumpIf(jumpIfAlsoCollectingNextTarget, () => this.pawn.IsCarryingThing(this.ThingToCarry));
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil2 in this.GotoTargetMap(CarryThingIndex)) yield return toil2;
            }
            yield return getToHaulTarget;
            yield return uninstallIfMinifiable;
            yield return startCarryingThing;
            yield return jumpIfAlsoCollectingNextTarget;
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil2 in this.GotoTargetMap(DestIndex)) yield return toil2;
            }
            yield return carryToContainer;
            yield return Toils_Goto.MoveOffTargetBlueprint(TargetIndex.B);
            Toil toil = Toils_General.Wait(this.Duration, TargetIndex.B);
            toil.WithProgressBarToilDelay(TargetIndex.B, false, -0.5f);
            EffecterDef workEffecter = this.WorkEffecter;
            if (workEffecter != null)
            {
                toil.WithEffect(workEffecter, TargetIndex.B, null);
            }
            SoundDef workSustainer = this.WorkSustainer;
            if (workSustainer != null)
            {
                toil.PlaySustainerOrSound(workSustainer, 1f);
            }
            Thing destThing = this.job.GetTarget(TargetIndex.B).Thing;
            toil.tickAction = delegate ()
            {
                if (this.pawn.IsHashIntervalTick(80) && destThing is Building_Grave && this.graveDigEffect == null)
                {
                    this.graveDigEffect = EffecterDefOf.BuryPawn.Spawn();
                    this.graveDigEffect.Trigger(destThing, destThing, -1);
                }
                Effecter effecter = this.graveDigEffect;
                if (effecter == null)
                {
                    return;
                }
                effecter.EffectTick(destThing, destThing);
            };
            this.ModifyPrepareToil(toil);
            yield return toil;
            yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B, TargetIndex.C);
            yield return ToilsAcrossMaps.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.C, null);
            yield return Toils_Haul.JumpToCarryToNextContainerIfPossible(carryToContainer, TargetIndex.C);
            yield break;
        }

        private void UpdateEnrouteTrackers()
        {
            int count = this.job.count;
            this.TryReserveEnroute(base.TargetThingC, ref count);
            if (base.TargetB != base.TargetC)
            {
                this.TryReserveEnroute(base.TargetThingB, ref count);
            }
            if (this.job.targetQueueB != null)
            {
                foreach (LocalTargetInfo a in this.job.targetQueueB)
                {
                    if (!base.TargetC.HasThing || !(a == base.TargetThingC))
                    {
                        this.TryReserveEnroute(a.Thing, ref count);
                    }
                }
            }
        }

        private void TryReserveEnroute(Thing thing, ref int count)
        {
            IHaulEnroute container;
            if ((container = (thing as IHaulEnroute)) != null && !thing.DestroyedOrNull())
            {
                this.UpdateTracker(container, ref count);
            }
        }

        private void UpdateTracker(IHaulEnroute container, ref int count)
        {
            if (this.ThingToCarry.DestroyedOrNull())
            {
                return;
            }
            if (this.job.playerForced && container.GetSpaceRemainingWithEnroute(this.ThingDef, null) == 0)
            {
                container.Map.enrouteManager.InterruptEnroutePawns(container, this.pawn);
            }
            int num = Mathf.Min(count, container.GetSpaceRemainingWithEnroute(this.ThingDef, null));
            if (num > 0)
            {
                container.Map.enrouteManager.AddEnroute(container, this.pawn, base.TargetThingA.def, num);
            }
            count -= num;
        }

        private Effecter graveDigEffect;

        protected const TargetIndex CarryThingIndex = TargetIndex.A;

        public const TargetIndex DestIndex = TargetIndex.B;

        protected const TargetIndex PrimaryDestIndex = TargetIndex.C;

        protected const int DiggingEffectInterval = 80;
    }
}
