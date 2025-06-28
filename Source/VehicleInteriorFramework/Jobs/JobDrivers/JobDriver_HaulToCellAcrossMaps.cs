using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_HaulToCellAcrossMaps : JobDriverAcrossMaps
    {
        public Thing ToHaul
        {
            get
            {
                return job.GetTarget(HaulableInd).Thing;
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
            Scribe_Values.Look<bool>(ref forbiddenInitially, "forbiddenInitially", false, false);
        }

        public override string GetReport()
        {
            IntVec3 cell = job.targetB.Cell;
            Thing thing = null;
            if (pawn.CurJob == job && pawn.carryTracker.CarriedThing != null)
            {
                thing = pawn.carryTracker.CarriedThing;
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
            if (!cell.InBounds(DestMap))
            {
                return null;
            }
            SlotGroup slotGroup = cell.GetSlotGroup(DestMap);
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
            return DestMap.reservationManager.Reserve(pawn, job, job.GetTarget(StoreCellInd), 1, -1, null, errorOnFailed, false) && TargetAMap.reservationManager.Reserve(pawn, job, job.GetTarget(HaulableInd), 1, -1, null, errorOnFailed, false);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            if (base.TargetThingA != null)
            {
                forbiddenInitially = base.TargetThingA.IsForbidden(pawn);
                return;
            }
            forbiddenInitially = false;
        }

        protected virtual Toil BeforeDrop()
        {
            return Toils_General.Label();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(HaulableInd);
            this.FailOnBurningImmobile(StoreCellInd, DestMap);

            if (!forbiddenInitially)
            {
                this.FailOnForbidden(HaulableInd);
            }
            yield return Toils_General.DoAtomic(delegate
            {
                startTick = Find.TickManager.TicksGame;
            });
            Toil reserveTargetA = ToilsAcrossMaps.Reserve(HaulableInd, 1, -1, null, false);
            yield return reserveTargetA;
            Toil postCarry = Toils_General.Label();
            yield return Toils_Jump.JumpIf(postCarry, delegate
            {
                Thing carriedThing;
                return (carriedThing = pawn.carryTracker.CarriedThing) != null && carriedThing == pawn.jobs.curJob.GetTarget(HaulableInd).Thing;
            });
            yield return Toils_General.DoAtomic(delegate
            {
                if (DropCarriedThingIfNotTarget && pawn.IsCarrying())
                {
                    if (DebugViewSettings.logCarriedBetweenJobs)
                    {
                        Log.Message(string.Format("Dropping {0} because it is not the designated Thing to haul.", pawn.carryTracker.CarriedThing));
                    }
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing thing, null);
                }
            });
            if (ShouldEnterTargetAMap)
            {
                foreach (var toil in GotoTargetMap(HaulableInd)) yield return toil;
            }
            var destMap = DestMap;
            Toil toilGoto = null;
            toilGoto = Toils_Goto.GotoThing(HaulableInd, PathEndMode.ClosestTouch, true).FailOnSomeonePhysicallyInteracting(HaulableInd).FailOn(delegate ()
            {
                Pawn actor = toilGoto.actor;
                Job curJob = actor.jobs.curJob;
                if (curJob.haulMode == HaulMode.ToCellStorage)
                {
                    Thing thing = curJob.GetTarget(HaulableInd).Thing;
                    var cell = actor.jobs.curJob.GetTarget(StoreCellInd).Cell;
                    if (destMap != null && !actor.jobs.curJob.GetTarget(StoreCellInd).Cell.IsValidStorageFor(destMap, thing))
                    {
                        return true;
                    }
                }
                return false;
            });
            yield return toilGoto;
            yield return Toils_Haul.StartCarryThing(HaulableInd, false, true, false, true, HaulAIUtility.IsInHaulableInventory(ToHaul));
            yield return postCarry;
            if (job.haulOpportunisticDuplicates)
            {
                yield return ToilsAcrossMaps.CheckForGetOpportunityDuplicate(reserveTargetA, HaulableInd, StoreCellInd, DestMap, false, null);
            }
            if (ShouldEnterTargetBMap)
            {
                foreach (var toil in GotoTargetMap(StoreCellInd)) yield return toil;
            }
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(StoreCellInd, PathEndMode.ClosestTouch);
            yield return carryToCell;
            yield return PossiblyDelay();
            yield return BeforeDrop();
            yield return Toils_Haul.PlaceHauledThingInCell(StoreCellInd, carryToCell, true, false);
        }

        private Toil PossiblyDelay()
        {
            Toil toil = ToilMaker.MakeToil("PossiblyDelay");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate ()
            {
                if (Find.TickManager.TicksGame >= startTick + MinimumHaulingJobTicks)
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
