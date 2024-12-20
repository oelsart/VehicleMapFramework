using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    internal class JobDriver_HaulToCellAcrossMaps : JobDriverAcrossMaps
    {
        public Thing ToHaul
        {
            get
            {
                return this.job.GetTarget(HaulableInd).Thing;
            }
        }

        protected virtual bool DropCarriedThingIfNotTarget
        {
            get
            {
                return false;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.forbiddenInitially, "forbiddenInitially", false, false);
        }

        public override string GetReport()
        {
            IntVec3 cell = this.job.targetB.Cell;
            Thing thing = null;
            if (this.pawn.CurJob == this.job && this.pawn.carryTracker.CarriedThing != null)
            {
                thing = this.pawn.carryTracker.CarriedThing;
            }
            else if (base.TargetThingA != null && base.TargetThingA.Spawned)
            {
                thing = base.TargetThingA;
            }
            if (thing == null)
            {
                return "ReportHaulingUnknown".Translate();
            }
            string text = null;
            SlotGroup slotGroup = cell.GetSlotGroup(this.DestMap);
            if (slotGroup != null)
            {
                text = slotGroup.parent.SlotYielderLabel();
            }
            if (text != null)
            {
                return "ReportHaulingTo".Translate(thing.Label, text.Named("DESTINATION"), thing.Named("THING"));
            }
            return "ReportHauling".Translate(thing.Label, thing);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.DestMap.reservationManager.Reserve(this.pawn, this.job, this.job.GetTarget(StoreCellInd), 1, -1, null, errorOnFailed, false) && this.TargetAMap.reservationManager.Reserve(this.pawn, this.job, this.job.GetTarget(HaulableInd), 1, -1, null, errorOnFailed, false);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            if (base.TargetThingA != null)
            {
                this.forbiddenInitially = base.TargetThingA.IsForbidden(this.pawn);
                return;
            }
            this.forbiddenInitially = false;
        }

        protected virtual Toil BeforeDrop()
        {
            return Toils_General.Label();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(HaulableInd);
            this.FailOnBurningImmobile(StoreCellInd, this.DestMap);

            if (!this.forbiddenInitially)
            {
                this.FailOnForbidden(HaulableInd);
            }
            yield return Toils_General.DoAtomic(delegate
            {
                this.startTick = Find.TickManager.TicksGame;
            });
            Toil reserveTargetA = ToilsAcrossMaps.Reserve(HaulableInd, 1, -1, null, false);
            yield return reserveTargetA;
            Toil postCarry = Toils_General.Label();
            yield return Toils_Jump.JumpIf(postCarry, delegate
            {
                Thing carriedThing;
                return (carriedThing = this.pawn.carryTracker.CarriedThing) != null && carriedThing == this.pawn.jobs.curJob.GetTarget(HaulableInd).Thing;
            });
            yield return Toils_General.DoAtomic(delegate
            {
                if (this.DropCarriedThingIfNotTarget && this.pawn.IsCarrying())
                {
                    if (DebugViewSettings.logCarriedBetweenJobs)
                    {
                        Log.Message(string.Format("Dropping {0} because it is not the designated Thing to haul.", this.pawn.carryTracker.CarriedThing));
                    }
                    this.pawn.carryTracker.TryDropCarriedThing(this.pawn.Position, ThingPlaceMode.Near, out Thing thing, null);
                }
            });
            if (this.ShouldEnterTargetAMap)
            {
                foreach (var toil in this.GotoTargetMap(HaulableInd)) yield return toil;
            }
            var destMap = this.DestMap;
            Toil toilGoto = null;
            toilGoto = Toils_Goto.GotoThing(HaulableInd, PathEndMode.ClosestTouch, true).FailOnSomeonePhysicallyInteracting(HaulableInd).FailOn(delegate ()
            {
                Pawn actor = toilGoto.actor;
                Job curJob = actor.jobs.curJob;
                if (curJob.haulMode == HaulMode.ToCellStorage)
                {
                    Thing thing = curJob.GetTarget(HaulableInd).Thing;
                    if (destMap != null && !actor.jobs.curJob.GetTarget(StoreCellInd).Cell.IsValidStorageFor(destMap, thing))
                    {
                        return true;
                    }
                }
                return false;
            });
            yield return toilGoto;
            yield return Toils_Haul.StartCarryThing(HaulableInd, false, true, false, true, HaulAIUtility.IsInHaulableInventory(this.ToHaul));
            yield return postCarry;
            if (this.job.haulOpportunisticDuplicates)
            {
                yield return ToilsAcrossMaps.CheckForGetOpportunityDuplicate(reserveTargetA, HaulableInd, StoreCellInd, this.DestMap, false, null);
            }
            if (this.ShouldEnterTargetBMap)
            {
                foreach(var toil in this.GotoTargetMap(StoreCellInd)) yield return toil;
            }
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(StoreCellInd, PathEndMode.ClosestTouch);
            yield return carryToCell;
            yield return this.PossiblyDelay();
            yield return this.BeforeDrop();
            yield return Toils_Haul.PlaceHauledThingInCell(StoreCellInd, carryToCell, true, false);
        }

        private Toil PossiblyDelay()
        {
            Toil toil = ToilMaker.MakeToil("PossiblyDelay");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate ()
            {
                if (Find.TickManager.TicksGame >= this.startTick + MinimumHaulingJobTicks)
                {
                    base.ReadyForNextToil();
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private bool forbiddenInitially;

        private const TargetIndex HaulableInd = TargetIndex.A;

        private const TargetIndex StoreCellInd = TargetIndex.B;

        private const int MinimumHaulingJobTicks = 30;
    }
}
