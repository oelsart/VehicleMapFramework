using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_TendPatientAcrossMaps : JobDriverAcrossMaps
    {
        protected Thing MedicineUsed
        {
            get
            {
                return this.job.targetB.Thing;
            }
        }

        protected Pawn Deliveree
        {
            get
            {
                return this.job.targetA.Pawn;
            }
        }

        protected bool IsMedicineInDoctorInventory
        {
            get
            {
                return this.MedicineUsed != null && this.pawn.inventory.Contains(this.MedicineUsed);
            }
        }

        protected Pawn_InventoryTracker MedicineHolderInventory
        {
            get
            {
                Thing medicineUsed = this.MedicineUsed;
                return (medicineUsed?.ParentHolder) as Pawn_InventoryTracker;
            }
        }

        protected Pawn OtherPawnMedicineHolder
        {
            get
            {
                return this.job.targetC.Pawn;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.usesMedicine, "usesMedicine", false, false);
            Scribe_Values.Look<PathEndMode>(ref this.pathEndMode, "pathEndMode", PathEndMode.None, false);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            this.usesMedicine = (this.MedicineUsed != null);
            if (this.Deliveree == this.pawn)
            {
                this.pathEndMode = PathEndMode.OnCell;
                return;
            }
            if (this.Deliveree.InBed())
            {
                this.pathEndMode = PathEndMode.InteractionCell;
                return;
            }
            if (this.Deliveree != this.pawn)
            {
                this.pathEndMode = PathEndMode.ClosestTouch;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.Deliveree != this.pawn && !this.pawn.Reserve(this.Deliveree.Map, this.Deliveree, this.job, 1, -1, null, errorOnFailed, false))
            {
                return false;
            }
            if (this.MedicineUsed != null)
            {
                int num = this.MedicineUsed.Map.reservationManager.CanReserveStack(this.pawn, this.MedicineUsed, 10, null, false);
                if (num <= 0 || !this.pawn.Reserve(this.MedicineUsed.Map, this.MedicineUsed, this.job, 10, Mathf.Min(num, Medicine.GetMedicineCountToFullyHeal(this.Deliveree)), null, errorOnFailed, false))
                {
                    return false;
                }
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => (this.MedicineUsed != null && this.pawn.Faction == Faction.OfPlayer && this.Deliveree.playerSettings != null && !this.Deliveree.playerSettings.medCare.AllowsMedicine(this.MedicineUsed.def)) || (this.pawn == this.Deliveree && this.pawn.Faction == Faction.OfPlayer && this.pawn.playerSettings != null && !this.pawn.playerSettings.selfTend));
            base.AddEndCondition(delegate
            {
                if (this.pawn.Faction == Faction.OfPlayer && HealthAIUtility.ShouldBeTendedNowByPlayer(this.Deliveree))
                {
                    return JobCondition.Ongoing;
                }
                if ((this.job.playerForced || this.pawn.Faction != Faction.OfPlayer) && this.Deliveree.health.HasHediffsNeedingTend(false))
                {
                    return JobCondition.Ongoing;
                }
                return JobCondition.Succeeded;
            });
            this.FailOnAggroMentalState(TargetIndex.A);
            Toil reserveMedicine = null;
            Toil gotoDestMap = ToilMaker.MakeToil("gotoDestMap");
            gotoDestMap.initAction = () =>
            {
                if (gotoDestMap.actor.CanReach(this.Deliveree, PathEndMode.InteractionCell, Danger.Deadly, false, false, TraverseMode.ByPawn, this.Deliveree.Map, out var exitSpot, out var enterSpot))
                {
                    var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps);
                    var driver = job.GetCachedDriver(this.pawn) as JobDriverAcrossMaps;
                    driver.SetSpots(exitSpot, enterSpot);
                    this.pawn.jobs.StartJob(job, JobCondition.Ongoing, null, true);
                }
            };
            Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, this.pathEndMode, false);
            if (this.usesMedicine)
            {
                List<Toil> list = JobDriver_TendPatientAcrossMaps.CollectMedicineToils(this.pawn, this.Deliveree, this.job, this, gotoDestMap, out reserveMedicine);
                foreach (Toil toil in list)
                {
                    yield return toil;
                }
            }
            yield return gotoDestMap;
            yield return gotoToil;
            int ticks = (int)(1f / this.pawn.GetStatValue(StatDefOf.MedicalTendSpeed, true, -1) * 600f);
            Toil waitToil;
            if (!this.job.draftedTend || this.pawn == base.TargetPawnA)
            {
                waitToil = Toils_General.Wait(ticks, TargetIndex.None);
            }
            else
            {
                waitToil = Toils_General.WaitWith_NewTemp(TargetIndex.A, ticks, false, true, false, TargetIndex.A, this.pathEndMode);
                waitToil.AddFinishAction(delegate
                {
                    if (this.Deliveree != null && this.Deliveree != this.pawn && this.Deliveree.CurJob != null && (this.Deliveree.CurJob.def == JobDefOf.Wait || this.Deliveree.CurJob.def == JobDefOf.Wait_MaintainPosture))
                    {
                        this.Deliveree.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
                    }
                });
            }
            waitToil.WithProgressBarToilDelay(TargetIndex.A, false, -0.5f).PlaySustainerOrSound(SoundDefOf.Interact_Tend, 1f);
            waitToil.activeSkill = (() => SkillDefOf.Medicine);
            waitToil.handlingFacing = true;
            waitToil.tickAction = delegate ()
            {
                if (this.pawn == this.Deliveree && this.pawn.Faction != Faction.OfPlayer && this.pawn.IsHashIntervalTick(100) && !this.pawn.Position.Fogged(this.pawn.Map))
                {
                    FleckMaker.ThrowMetaIcon(this.pawn.Position, this.pawn.Map, FleckDefOf.HealingCross, 0.42f);
                }
                if (this.pawn != this.Deliveree)
                {
                    this.pawn.rotationTracker.FaceTarget(this.Deliveree);
                }
            };
            waitToil.FailOn(() => this.pawn != this.Deliveree && !this.pawn.CanReachImmediate(this.Deliveree.SpawnedParentOrMe, this.pathEndMode));
            yield return Toils_Jump.JumpIf(waitToil, () => !this.usesMedicine || !this.IsMedicineInDoctorInventory);
            yield return Toils_Tend.PickupMedicine(TargetIndex.B, this.Deliveree).FailOnDestroyedOrNull(TargetIndex.B);
            yield return waitToil;
            yield return Toils_Tend.FinalizeTend(this.Deliveree);
            if (this.usesMedicine)
            {
                yield return JobDriver_TendPatientAcrossMaps.FindMoreMedicineToil_NewTemp(this.pawn, this.Deliveree, TargetIndex.B, this.job, reserveMedicine);
            }
            yield return Toils_Jump.Jump(gotoDestMap);
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            if (dinfo.Def.ExternalViolenceFor(this.pawn) && this.pawn.Faction != Faction.OfPlayer && this.pawn == this.Deliveree)
            {
                this.pawn.jobs.CheckForJobOverride(0f);
            }
        }

        public static List<Toil> CollectMedicineToils(Pawn doctor, Pawn patient, Job job, JobDriverAcrossMaps driver, Toil gotoToil, out Toil reserveMedicine)
        {
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Clear();
            var medicineUsed = job.targetB.Thing;
            var medicineHolderInventory = (medicineUsed?.ParentHolder) as Pawn_InventoryTracker;
            var otherPawnMedicineHolder = job.targetC.Pawn;
            reserveMedicine = Toils_Tend.ReserveMedicine(TargetIndex.B, patient).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_Jump.JumpIf(gotoToil, () => medicineUsed != null && doctor.inventory.Contains(medicineUsed)));
            if (driver.ShouldEnterTargetBMap)
            {
                foreach (var toil2 in driver.GotoTargetMap(MedicineIndex)) JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(toil2);
            }
            Toil toil = Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch, false).FailOn(delegate ()
            {
                return otherPawnMedicineHolder != (medicineHolderInventory?.pawn) || otherPawnMedicineHolder.IsForbidden(doctor);
            });
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_Haul.CheckItemCarriedByOtherPawn(medicineUsed, TargetIndex.C, toil));
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(reserveMedicine);
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch, false).FailOnDespawnedNullOrForbidden(TargetIndex.B));
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_Tend.PickupMedicine(TargetIndex.B, patient).FailOnDestroyedOrNull(TargetIndex.B));
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_Haul.CheckForGetOpportunityDuplicate(reserveMedicine, TargetIndex.B, TargetIndex.None, true, null));
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_Jump.Jump(gotoToil));
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(toil);
            JobDriver_TendPatientAcrossMaps.tmpCollectToils.Add(Toils_General.Wait(25, TargetIndex.None).WithProgressBarToilDelay(TargetIndex.C, false, -0.5f));
            List<Toil> list = JobDriver_TendPatientAcrossMaps.tmpCollectToils;
            Thing medicineUsed2 = medicineUsed;
            ThingOwner innerContainer = doctor.inventory.innerContainer;
            list.Add(Toils_Haul.TakeFromOtherInventory(medicineUsed2, innerContainer, medicineHolderInventory?.innerContainer, Medicine.GetMedicineCountToFullyHeal(patient), TargetIndex.B));
            return JobDriver_TendPatientAcrossMaps.tmpCollectToils;
        }

        public static Toil FindMoreMedicineToil(Pawn doctor, Pawn patient, Thing medicine, Job job, Toil reserveMedicine)
        {
            return JobDriver_TendPatientAcrossMaps.FindMoreMedicineToil_NewTemp(doctor, patient, TargetIndex.B, job, reserveMedicine);
        }

        public static Toil FindMoreMedicineToil_NewTemp(Pawn doctor, Pawn patient, TargetIndex medicineIndex, Job job, Toil reserveMedicine)
        {
            Toil toil = ToilMaker.MakeToil("FindMoreMedicineToil_NewTemp");
            toil.initAction = delegate ()
            {
                if (job.GetTarget(medicineIndex).Thing.DestroyedOrNull())
                {
                    Thing thing = HealthAIAcrossMapsUtility.FindBestMedicine(doctor, patient, false, out var exitSpot, out var enterSpot);
                    if (thing != null)
                    {
                        var gotoDestMap = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps);
                        var driver = gotoDestMap.GetCachedDriver(doctor) as JobDriverAcrossMaps;
                        driver.SetSpots(exitSpot, enterSpot);
                        doctor.jobs.StartJob(gotoDestMap, JobCondition.Ongoing, null, true);
                        job.SetTarget(medicineIndex, thing);
                        doctor.jobs.curDriver.JumpToToil(reserveMedicine);
                    }
                }
            };
            return toil;
        }

        private bool usesMedicine;

        private PathEndMode pathEndMode;

        public const int BaseTendDuration = 600;

        private const int TicksBetweenSelfTendMotes = 100;

        private const TargetIndex MedicineIndex = TargetIndex.B;

        private const TargetIndex MedicineHolderIndex = TargetIndex.C;

        private static List<Toil> tmpCollectToils = new List<Toil>();
    }
}
