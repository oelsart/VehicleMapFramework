using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_TendEntityAcrossMaps : JobDriverAcrossMaps
    {
        private Thing Platform
        {
            get
            {
                return base.TargetThingA;
            }
        }

        private Pawn InnerPawn
        {
            get
            {
                Building_HoldingPlatform building_HoldingPlatform = this.Platform as Building_HoldingPlatform;
                if (building_HoldingPlatform == null)
                {
                    return null;
                }
                return building_HoldingPlatform.HeldPawn;
            }
        }

        private Thing MedicineUsed
        {
            get
            {
                return this.job.targetB.Thing;
            }
        }

        protected bool IsMedicineInDoctorInventory
        {
            get
            {
                return this.MedicineUsed != null && this.pawn.inventory.Contains(this.MedicineUsed);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.usesMedicine, "usesMedicine", false, false);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            this.usesMedicine = (this.MedicineUsed != null);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!this.pawn.Reserve(this.Platform, this.job, 1, -1, null, errorOnFailed, false))
            {
                return false;
            }
            if (this.usesMedicine)
            {
                int num = this.pawn.Map.reservationManager.CanReserveStack(this.pawn, this.MedicineUsed, 10, null, false);
                if (num <= 0)
                {
                    return false;
                }
                int stackCount = Mathf.Min(num, Medicine.GetMedicineCountToFullyHeal(this.InnerPawn));
                if (!this.pawn.Reserve(this.MedicineUsed, this.job, 10, stackCount, null, errorOnFailed, false))
                {
                    return false;
                }
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(delegate ()
            {
                Pawn innerPawn = this.InnerPawn;
                if (innerPawn == null || innerPawn.Destroyed)
                {
                    return true;
                }
                if (this.MedicineUsed != null && innerPawn.playerSettings != null && !innerPawn.playerSettings.medCare.AllowsMedicine(this.MedicineUsed.def))
                {
                    return true;
                }
                CompHoldingPlatformTarget compHoldingPlatformTarget = innerPawn.TryGetComp<CompHoldingPlatformTarget>();
                return compHoldingPlatformTarget != null && compHoldingPlatformTarget.containmentMode == EntityContainmentMode.Release;
            });
            base.AddEndCondition(delegate
            {
                if (HealthAIUtility.ShouldBeTendedNowByPlayer(this.InnerPawn))
                {
                    return JobCondition.Ongoing;
                }
                if (this.job.playerForced && this.InnerPawn.health.HasHediffsNeedingTend(false))
                {
                    return JobCondition.Ongoing;
                }
                return JobCondition.Succeeded;
            });
            Toil reserveMedicine = null;
            Toil gotoDestMap = ToilMaker.MakeToil("gotoDestMap");
            gotoDestMap.initAction = () =>
            {
                if (gotoDestMap.actor.CanReach(this.Platform, PathEndMode.InteractionCell, Danger.Deadly, false, false, TraverseMode.ByPawn, this.Platform.Map, out var exitSpot, out var enterSpot))
                {
                    var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps);
                    var driver = job.GetCachedDriver(this.pawn) as JobDriverAcrossMaps;
                    driver.SetSpots(exitSpot, enterSpot);
                    this.pawn.jobs.StartJob(job, JobCondition.Ongoing, null, true);
                }
            };
            Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, false).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            if (this.usesMedicine)
            {
                List<Toil> list = JobDriver_TendPatientAcrossMaps.CollectMedicineToils(this.pawn, this.InnerPawn, this.job, this, gotoDestMap, out reserveMedicine);
                foreach (Toil toil in list)
                {
                    yield return toil;
                }
            }
            yield return gotoDestMap;
            yield return gotoToil;
            int ticks = (int)(1f / this.pawn.GetStatValue(StatDefOf.MedicalTendSpeed, true, -1) * 600f);
            Toil waitToil = Toils_General.WaitWith(TargetIndex.A, ticks, true, false, false, TargetIndex.A);
            waitToil.activeSkill = (() => SkillDefOf.Medicine);
            waitToil.FailOnCannotTouch(TargetIndex.A, PathEndMode.ClosestTouch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f).PlaySustainerOrSound(SoundDefOf.Interact_Tend, 1f);
            yield return Toils_Jump.JumpIf(waitToil, () => !this.usesMedicine || !this.IsMedicineInDoctorInventory);
            yield return Toils_Tend.PickupMedicine(TargetIndex.B, this.InnerPawn).FailOnDestroyedOrNull(TargetIndex.B);
            yield return waitToil;
            yield return Toils_Tend.FinalizeTend(this.InnerPawn);
            if (this.usesMedicine)
            {
                yield return JobDriver_TendPatientAcrossMaps.FindMoreMedicineToil_NewTemp(this.pawn, this.InnerPawn, TargetIndex.B, this.job, reserveMedicine);
            }
            yield return Toils_Jump.Jump(gotoDestMap);
        }

        public override string GetReport()
        {
            return JobUtility.GetResolvedJobReport(this.job.def.reportString, this.InnerPawn, this.job.targetB, this.job.targetC);
        }

        private bool usesMedicine;

        private const TargetIndex PlatformIndex = TargetIndex.A;

        private const TargetIndex MedicineIndex = TargetIndex.B;

        private const int MaxMedicineReservations = 10;
    }
}
