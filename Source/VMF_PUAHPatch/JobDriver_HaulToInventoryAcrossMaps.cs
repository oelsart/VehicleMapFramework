using PickUpAndHaul;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleInteriors;
using Verse;
using Verse.AI;

namespace VMF_PUAHPatch;

public class JobDriver_HaulToInventoryAcrossMaps : JobDriverAcrossMaps
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Log.Message($"{pawn} starting HaulToInventory job: {job.targetQueueA.ToStringSafeEnumerable()}:{job.countQueue.ToStringSafeEnumerable()}");
        pawn.ReserveAsManyAsPossible(TargetAMap, job.targetQueueA, job);
        pawn.ReserveAsManyAsPossible(DestMap, job.targetQueueB, job);
        return pawn.Reserve(TargetAMap, job.targetQueueA[0], job) && pawn.Reserve(DestMap, job.targetB, job);
    }

    //get next, goto, take, check for more. Branches off to "all over the place"
    protected override IEnumerable<Toil> MakeNewToils()
    {
        var takenToInventory = pawn.TryGetComp<CompHauledToInventory>();
        var wait = Toils_General.Wait(2);

        if (base.ShouldEnterTargetAMap)
        {
            foreach (var toil in base.GotoTargetMap(TargetIndex.A)) yield return toil;
        }

        var nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A); //also does count
        yield return nextTarget;

        yield return CheckForOverencumberedForCombatExtended();

        var gotoThing = new Toil
        {
            initAction = () => pawn.pather.StartPath(TargetThingA, PathEndMode.ClosestTouch),
            defaultCompleteMode = ToilCompleteMode.PatherArrival
        };
        gotoThing.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return gotoThing;

        var takeThing = new Toil
        {
            initAction = () =>
            {
                var actor = pawn;
                var thing = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                Toils_Haul.ErrorCheckForCarry(actor, thing);

                //get max we can pick up
                var countToPickUp = Mathf.Min(job.count, MassUtility.CountToPickUpUntilOverEncumbered(actor, thing));
                Log.Message($"{actor} is hauling to inventory {thing}:{countToPickUp}");

                if (ModCompatibilityCheck.CombatExtendedIsActive)
                {
                    countToPickUp = (int)CompatHelperInvoker.CanFitInInventory(null, pawn, thing);
                }

                if (countToPickUp > 0)
                {
                    var splitThing = thing.SplitOff(countToPickUp);
                    var shouldMerge = takenToInventory.GetHashSet().Any(x => x.def == thing.def);
                    actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, shouldMerge);
                    takenToInventory.RegisterHauledItem(splitThing);

                    if (ModCompatibilityCheck.CombatExtendedIsActive)
                    {
                        CompatHelperInvoker.UpdateInventory(null, pawn);
                    }
                }

                //thing still remains, so queue up hauling if we can + end the current job (smooth/instant transition)
                //This will technically release the reservations in the queue, but what can you do
                if (thing.Spawned)
                {
                    var haul = HaulAIUtility.HaulToStorageJob(actor, thing, false);
                    if (haul?.TryMakePreToilReservations(actor, false) ?? false)
                    {
                        actor.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
                    }
                    actor.jobs.curDriver.JumpToToil(wait);
                }
            }
        };
        yield return takeThing;
        yield return Toils_Jump.JumpIf(nextTarget, () => !job.targetQueueA.NullOrEmpty());

        //Find more to haul, in case things spawned while this was in progess
        yield return new Toil
        {
            initAction = () =>
            {
                var haulables = TempListForThings;
                haulables.Clear();
                haulables.AddRange(pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling());
                var haulMoreWork = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(wg => wg.Worker is WorkGiver_HaulToInventory).Worker as WorkGiver_HaulToInventory;
                Job haulMoreJob = null;
                var haulMoreThing = WorkGiver_HaulToInventory.GetClosestAndRemove(pawn.Position, pawn.Map, haulables, PathEndMode.ClosestTouch,
                       TraverseParms.For(pawn), 12, t => (haulMoreJob = haulMoreWork.JobOnThing(pawn, t)) != null);

                //WorkGiver_HaulToInventory found more work nearby
                if (haulMoreThing != null)
                {
                    Log.Message($"{pawn} hauling again : {haulMoreThing}");
                    if (haulMoreJob.TryMakePreToilReservations(pawn, false))
                    {
                        pawn.jobs.jobQueue.EnqueueFirst(haulMoreJob, JobTag.Misc);
                        EndJobWith(JobCondition.Succeeded);
                    }
                }
            }
        };

        if (base.ShouldEnterTargetBMap)
        {
            foreach (var toil in base.GotoTargetMap(TargetIndex.B)) yield return toil;
        }

        //maintain cell reservations on the trip back
        //TODO: do that when we carry things
        //I guess that means TODO: implement carrying the rest of the items in this job instead of falling back on HaulToStorageJob
        yield return TargetB.HasThing ? Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
            : Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.ClosestTouch);

        yield return new Toil //Queue next job
        {
            initAction = () =>
            {
                var actor = pawn;
                var curJob = actor.jobs.curJob;
                var storeCell = curJob.targetB;

                var unloadJob = JobMaker.MakeJob(PickUpAndHaulJobDefOf.UnloadYourHauledInventory, storeCell);
                if (unloadJob.TryMakePreToilReservations(actor, false))
                {
                    actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);
                    EndJobWith(JobCondition.Succeeded);
                    //This will technically release the cell reservations in the queue, but what can you do
                }
            }
        };
        yield return wait;
    }

    private static List<Thing> TempListForThings { get; } = new List<Thing>();

    /// <summary>
    /// the workgiver checks for encumbered, this is purely extra for CE
    /// </summary>
    /// <returns></returns>
    public Toil CheckForOverencumberedForCombatExtended()
    {
        var toil = new Toil();

        if (!ModCompatibilityCheck.CombatExtendedIsActive)
        {
            return toil;
        }

        toil.initAction = () =>
        {
            var actor = toil.actor;
            var curJob = actor.jobs.curJob;
            var nextThing = curJob.targetA.Thing;

            var ceOverweight = (bool)CompatHelperInvoker.CeOverweight(null, pawn);

            if (!(MassUtility.EncumbrancePercent(actor) <= 0.9f && !ceOverweight))
            {
                var haul = HaulAIUtility.HaulToStorageJob(actor, nextThing, false);
                if (haul?.TryMakePreToilReservations(actor, false) ?? false)
                {
                    //note that HaulToStorageJob etc doesn't do opportunistic duplicate hauling for items in valid storage. REEEE
                    actor.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
                    EndJobWith(JobCondition.Succeeded);
                }
            }
        };

        return toil;
    }
}
