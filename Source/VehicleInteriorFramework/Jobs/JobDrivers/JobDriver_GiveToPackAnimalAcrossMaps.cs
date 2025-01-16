using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors
{
    public class JobDriver_GiveToPackAnimalAcrossMaps : JobDriverAcrossMaps
    {
		private Thing Item
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        private Pawn Animal
        {
            get
            {
                return (Pawn)this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Item.MapHeld, this.Item, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            Toil findNearestCarrier = this.FindCarrierToil();
            yield return findNearestCarrier;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch, false).FailOnDespawnedNullOrForbidden(TargetIndex.B).JumpIf(() => !this.CanCarryAtLeastOne(this.Animal), findNearestCarrier);
            yield return this.GiveToCarrierAsMuchAsPossibleToil();
            yield return Toils_Jump.JumpIf(findNearestCarrier, () => this.pawn.carryTracker.CarriedThing != null);
            yield break;
        }

        private Toil FindCarrierToil()
        {
            Toil toil = ToilMaker.MakeToil("FindCarrierToil");
            toil.initAction = delegate ()
            {
                Pawn pawn = this.FindCarrier();
                if (pawn == null)
                {
                    this.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                    return;
                }
                this.job.SetTarget(TargetIndex.B, pawn);
                this.pawn.CanReach(pawn, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, pawn.Map, out var exitSpot, out var enterSpot);
                var gotoDestMap = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps);
                var driver = gotoDestMap.GetCachedDriver(toil.actor) as JobDriverAcrossMaps;
                driver.SetSpots(exitSpot, enterSpot);
                toil.actor.jobs.StartJob(gotoDestMap, JobCondition.Ongoing, null, true);
            };
            return toil;
        }

        private Pawn FindCarrier()
        {
            IEnumerable<Pawn> enumerable = JobDriver_GiveToPackAnimalAcrossMaps.CarrierCandidatesFor(this.pawn);
            Pawn animal = this.Animal;
            if (animal != null && enumerable.Contains(animal) && animal.RaceProps.packAnimal && this.CanCarryAtLeastOne(animal))
            {
                return animal;
            }
            Pawn pawn = null;
            float num = -1f;
            foreach (Pawn pawn2 in enumerable)
            {
                if (pawn2.RaceProps.packAnimal && this.CanCarryAtLeastOne(pawn2))
                {
                    float num2 = (float)pawn2.PositionOnBaseMap().DistanceToSquared(this.pawn.PositionOnBaseMap());
                    if (pawn == null || num2 < num)
                    {
                        pawn = pawn2;
                        num = num2;
                    }
                }
            }
            return pawn;
        }

        public static IEnumerable<Pawn> CarrierCandidatesFor(Pawn pawn)
        {
            var baseMap = pawn.BaseMap();
            IEnumerable<Pawn> enumerable = pawn.IsFormingCaravan() ? pawn.GetLord().ownedPawns : baseMap.mapPawns.SpawnedPawnsInFaction(pawn.Faction).Concat(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany(v => v.VehicleMap.mapPawns.SpawnedPawnsInFaction(pawn.Faction)));
            enumerable = from x in enumerable
                         where x.RaceProps.packAnimal && !x.inventory.UnloadEverything
                         select x;
            if (pawn.Map.IsPlayerHome)
            {
                enumerable = from x in enumerable
                             where x.IsFormingCaravan()
                             select x;
            }
            return enumerable;
        }

        private bool CanCarryAtLeastOne(Pawn carrier)
        {
            return !MassUtility.WillBeOverEncumberedAfterPickingUp(carrier, this.Item, 1);
        }

        private Toil GiveToCarrierAsMuchAsPossibleToil()
        {
            Toil toil = ToilMaker.MakeToil("GiveToCarrierAsMuchAsPossibleToil");
            toil.initAction = delegate ()
            {
                if (this.Item == null)
                {
                    this.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                    return;
                }
                int count = Mathf.Min(MassUtility.CountToPickUpUntilOverEncumbered(this.Animal, this.Item), this.Item.stackCount);
                this.pawn.carryTracker.innerContainer.TryTransferToContainer(this.Item, this.Animal.inventory.innerContainer, count, true);
            };
            return toil;
        }

        private const TargetIndex ItemInd = TargetIndex.A;

        private const TargetIndex AnimalInd = TargetIndex.B;
    }
}
