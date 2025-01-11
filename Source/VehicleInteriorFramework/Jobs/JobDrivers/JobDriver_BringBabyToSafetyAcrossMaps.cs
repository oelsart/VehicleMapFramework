using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_BringBabyToSafetyAcrossMaps : JobDriverAcrossMaps
    {
        private Pawn Baby
        {
            get
            {
                return (Pawn)base.TargetThingA;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Baby.Map, this.Baby, this.job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            base.AddFailCondition(() => !this.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            base.AddFailCondition(delegate
            {
                ChildcareUtility.BreastfeedFailReason? breastfeedFailReason;
                return !ChildcareUtility.CanSuckle(this.Baby, out breastfeedFailReason);
            });
            if (!this.job.playerForced && this.Baby.Spawned && !this.Baby.ComfortableTemperatureAtCell(this.Baby.Position, this.Baby.Map) && (PawnUtility.ShouldSendNotificationAbout(this.pawn) || PawnUtility.ShouldSendNotificationAbout(this.Baby)))
            {
                yield return Toils_General.Do(delegate
                {
                    Messages.Message("MessageTakingBabyToSafeTemperature".Translate(this.pawn.Named("ADULT"), this.Baby.Named("BABY")), new LookTargets(new TargetInfo[]
                    {
                        this.pawn,
                        this.Baby
                    }), MessageTypeDefOf.NeutralEvent, true);
                });
            }
            Toil findBedForBaby = this.FindBedForBaby();
            yield return Toils_Jump.JumpIf(findBedForBaby, () => this.pawn.IsCarryingPawn(this.Baby)).FailOn(() => !this.pawn.IsCarryingPawn(this.Baby) && (this.pawn.Downed || this.pawn.Drafted));
            if (this.ShouldEnterTargetAMap)
            {
                foreach(var toil in this.GotoTargetMap(TargetIndex.A)) yield return toil;
            }
            foreach (Toil toil in JobDriver_PickupToHold.Toils(this, TargetIndex.A, true))
            {
                yield return toil;
            }
            yield return findBedForBaby;
            yield return Toils_Reserve.ReserveDestinationOrThing(TargetIndex.B);
            yield return Toils_Goto.Goto(TargetIndex.B, PathEndMode.OnCell).FailOnInvalidOrDestroyed(TargetIndex.B).FailOnForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOn(() => !this.pawn.IsCarryingPawn(this.Baby) || this.pawn.Downed || this.pawn.Drafted);
            yield return Toils_Reserve.ReleaseDestinationOrThing(TargetIndex.B);
            yield return Toils_Bed.TuckIntoBed(TargetIndex.B, TargetIndex.A, false);
        }

        private Toil FindBedForBaby()
        {
            Toil toil = ToilMaker.MakeToil("FindBedForBaby");
            toil.initAction = delegate ()
            {
                LocalTargetInfo pack = JobDriver_BringBabyToSafetyAcrossMaps.SafePlaceForBaby(this.Baby, this.pawn, false, out var exitSpot, out var enterSpot);
                LocalTargetInfo pack2 = LocalTargetInfo.Invalid;
                if (CaravanFormingUtility.IsFormingCaravanOrDownedPawnToBeTakenByCaravan(this.Baby))
                {
                    pack2 = JobGiver_PrepareCaravan_GatherDownedPawns.FindRandomDropCell(this.pawn, this.Baby);
                }
                if (pack.IsValid)
                {
                    if (pack2.IsValid && pack2.CellOnBaseMap().DistanceTo(this.pawn.PositionOnBaseMap()) < pack.CellOnBaseMap().DistanceTo(this.pawn.PositionOnBaseMap()))
                    {
                        return;
                    }
                    toil.actor.CurJob.SetTarget(TargetIndex.B, pack);
                    var job = toil.actor.CurJob.Clone();
                    toil.actor.jobs.StartJob(JobAcrossMapsUtility.GotoDestMapJob(toil.actor, exitSpot, enterSpot, job), keepCarryingThingOverride: true);
                    return;
                }
                else
                {
                    if (pack2.IsValid)
                    {
                        toil.actor.CurJob.SetTarget(TargetIndex.B, pack2);
                        return;
                    }
                    this.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
                    return;
                }
            };
            toil.FailOn(() => !this.pawn.IsCarryingPawn(this.Baby) || this.pawn.Downed || this.pawn.Drafted);
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static LocalTargetInfo SafePlaceForBaby(Pawn baby, Pawn hauler, bool ignoreOtherReservations, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = null;
            enterSpot = null;
            ChildcareUtility.BreastfeedFailReason? breastfeedFailReason;
            if (!ChildcareUtility.CanSuckle(baby, out breastfeedFailReason))
            {
                return LocalTargetInfo.Invalid;
            }
            if (!ChildcareUtility.CanHaulBabyNow(hauler, baby, ignoreOtherReservations, out breastfeedFailReason))
            {
                return LocalTargetInfo.Invalid;
            }
            Building_Bed building_Bed = baby.CurrentBed() ?? RestUtilityOnVehicle.FindBedFor(baby, hauler, true, false, baby.GuestStatus, out exitSpot, out enterSpot);
            float temperatureForCell = GenTemperature.GetTemperatureForCell((building_Bed != null) ? building_Bed.Position : IntVec3.Invalid, baby.MapHeld);
            if (building_Bed != null && building_Bed.Medical && HealthAIUtility.ShouldSeekMedicalRest(baby))
            {
                return building_Bed;
            }
            if (building_Bed != null && baby.ComfortableTemperatureRange().Includes(temperatureForCell))
            {
                return building_Bed;
            }
            LocalTargetInfo result = LocalTargetInfo.Invalid;
            if (ChildcareUtility.BabyNeedsMovingForTemperatureReasons(baby, hauler, out Region targetRegion, null))
            {
                result = RCellFinder.SpotToStandDuringJob(hauler, null, targetRegion);
            }
            else if (baby.Spawned)
            {
                result = baby.Position;
            }
            else
            {
                result = RCellFinder.SpotToStandDuringJob(hauler, null, baby.GetRegionHeld(RegionType.Set_Passable));
            }
            if (result.IsValid && baby.ComfortableTemperatureAtCell(result.Cell, baby.MapHeld))
            {
                return result;
            }
            if (building_Bed != null && baby.SafeTemperatureRange().Includes(temperatureForCell))
            {
                return building_Bed;
            }
            if (result.IsValid && baby.SafeTemperatureAtCell(result.Cell, baby.MapHeld))
            {
                return result;
            }
            LocalTargetInfo result2 = LocalTargetInfo.Invalid;
            if (baby.Spawned)
            {
                result2 = baby.Position;
            }
            else
            {
                result2 = RCellFinder.SpotToStandDuringJob(hauler, null, null);
            }
            if (building_Bed != null && GenTemperature.GetTemperatureForCell(result2.Cell, baby.MapHeld) < temperatureForCell + 5f)
            {
                return building_Bed;
            }
            return result2;
        }

        public static bool CanHaulBabyNow(Pawn hauler, Pawn baby, bool ignoreOtherReservations, out ChildcareUtility.BreastfeedFailReason? reason)
        {
            if (!ChildcareUtility.CanHaulBaby(hauler, baby, out reason))
            {
                return false;
            }
            if (!hauler.CanReserve(baby, baby.Map, 1, -1, null, ignoreOtherReservations))
            {
                reason = new ChildcareUtility.BreastfeedFailReason?(ChildcareUtility.BreastfeedFailReason.HaulerCannotReserveBaby);
            }
            else if (!hauler.CanReach(baby, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, baby.Map, out _, out _))
            {
                reason = new ChildcareUtility.BreastfeedFailReason?(ChildcareUtility.BreastfeedFailReason.HaulerCannotReachBaby);
            }
            return reason == null;
        }

        private const TargetIndex BabyInd = TargetIndex.A;

        private const TargetIndex BabyBedInd = TargetIndex.B;
    }
}