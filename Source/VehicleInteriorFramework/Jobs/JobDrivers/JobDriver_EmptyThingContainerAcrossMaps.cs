using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_EmptyThingContainerAcrossMaps : JobDriverAcrossMaps
    {
        protected virtual PathEndMode ContainerPathEndMode
        {
            get
            {
                if (!this.job.GetTarget(TargetIndex.A).Thing.def.hasInteractionCell)
                {
                    return PathEndMode.Touch;
                }
                return PathEndMode.InteractionCell;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetA.Thing.Map, this.job.GetTarget(TargetIndex.A), this.job, 1, -1, this.job.def.containerReservationLayer, errorOnFailed, false) && this.pawn.Reserve(this.DestMap, this.job.GetTarget(TargetIndex.C), this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (this.ShouldEnterTargetAMap)
            {
                foreach(var toil2 in this.GotoTargetMap(TargetIndex.A)) yield return toil2;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, this.ContainerPathEndMode, false).FailOnDespawnedNullOrForbidden(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A).FailOn(delegate ()
            {
                CompThingContainer compThingContainer;
                return this.job.GetTarget(TargetIndex.A).Thing.TryGetComp(out compThingContainer) && compThingContainer.Empty;
            });
            yield return Toils_General.WaitWhileExtractingContents(TargetIndex.A, TargetIndex.B, 120);
            yield return Toils_General.Do(delegate
            {
                if (base.TargetThingA.TryGetInnerInteractableThingOwner().TryDropAll(this.pawn.Position, this.pawn.Map, ThingPlaceMode.Near, null, null, true))
                {
                    CompThingContainer compThingContainer = base.TargetThingA.TryGetComp<CompThingContainer>();
                    if (compThingContainer == null)
                    {
                        return;
                    }
                    EffecterDef dropEffecterDef = compThingContainer.Props.dropEffecterDef;
                    if (dropEffecterDef == null)
                    {
                        return;
                    }
                    dropEffecterDef.Spawn(base.TargetThingA, base.Map, 1f).Cleanup();
                }
            });
            yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null, false);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch, false).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false, true, false);
            if (this.ShouldEnterTargetBMap)
            {
                foreach (var toil2 in this.GotoTargetMap(TargetIndex.B)) yield return toil2;
            }
            Toil toil = Toils_Haul.CarryHauledThingToCell(TargetIndex.C, PathEndMode.ClosestTouch);
            yield return toil;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, null, true, true);
            yield break;
        }

        protected const TargetIndex ContainerInd = TargetIndex.A;

        protected const TargetIndex ContentsInd = TargetIndex.B;

        protected const TargetIndex StoreCellInd = TargetIndex.C;

        private const int OpenTicks = 120;
    }
}
