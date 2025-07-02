using PickUpAndHaul;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VehicleInteriors;
using Verse;
using Verse.AI;

namespace VMF_PUAHPatch;

public class WorkGiver_HaulToInventoryAcrossMaps : WorkGiver_HaulToInventory, IWorkGiverAcrossMaps
{
    public bool NeedVirtualMapTransfer => false;

    //Thanks to AlexTD for the more dynamic search range
    //And queueing
    //And optimizing
    private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        List<Thing> list = pawn.Map.BaseMapAndVehicleMaps().SelectMany(m => m.listerHaulables.ThingsPotentiallyNeedingHauling()).ToList();
        WorkGiver_HaulToInventoryAcrossMaps.Comparer.rootCell = pawn.PositionOnBaseMap();
        list.Sort(WorkGiver_HaulToInventoryAcrossMaps.Comparer);
        return list;
    }

    private static ThingPositionComparerAcrossMaps Comparer { get; } = new ThingPositionComparerAcrossMaps();

    public class ThingPositionComparerAcrossMaps : IComparer<Thing>
    {
        public IntVec3 rootCell;
        public int Compare(Thing x, Thing y) => (x.PositionOnBaseMap() - rootCell).LengthHorizontalSquared.CompareTo((y.PositionOnBaseMap() - rootCell).LengthHorizontalSquared);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        => OkThingToHaul(thing, pawn)
        && IsNotCorpseOrAllowed(thing)
        && HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced, out _, out _)
        && StoreAcrossMapsUtility.TryFindBestBetterStorageFor(thing, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(thing), pawn.Faction, out _, out _, false, out _, out _);

    //pick up stuff until you can't anymore,
    //while you're up and about, pick up something and haul it
    //before you go out, empty your pockets
    public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        if (!OkThingToHaul(thing, pawn) || !HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced, out var exitSpot, out var enterSpot))
        {
            return null;
        }

        if (OverAllowedGearCapacity(pawn)
            || pawn.GetComp<CompHauledToInventory>() is null // Misc. Robots compatibility
                                                             // See https://github.com/catgirlfighter/RimWorld_CommonSense/blob/master/Source/CommonSense11/CommonSense/OpportunisticTasks.cs#L129-L140
            || !IsNotCorpseOrAllowed(thing) //This WorkGiver gets hijacked by AllowTool and expects us to urgently haul corpses.
            || MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, thing, 1)) //https://github.com/Mehni/PickUpAndHaul/pull/18
        {
            return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, thing, false, exitSpot, enterSpot);
        }

        var map = pawn.Map;
        var thingMap = thing.MapHeld;
        var designationManager = thingMap.designationManager;
        var currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
        ThingOwner nonSlotGroupThingOwner = null;
        StoreTarget storeTarget;
        Map destMap;
        if (StoreAcrossMapsUtility.TryFindBestBetterStorageFor(thing, pawn, thingMap, currentPriority, pawn.Faction, out var targetCell, out var haulDestination, true, out var exitSpot2, out var enterSpot2))
        {
            destMap = enterSpot2.Map ?? exitSpot2.Map.BaseMap() ?? enterSpot.Map ?? exitSpot.Map.BaseMap() ?? map;
            if (haulDestination is ISlotGroupParent)
            {
                //since we've gone through all the effort of getting the loc, might as well use it.
                //Don't multi-haul food to hoppers.
                if (HaulToHopperJob(thing, targetCell, destMap))
                {
                    return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, thing, false, exitSpot, enterSpot);
                }
                else
                {
                    storeTarget = new StoreTarget(targetCell);
                }
            }
            else if (haulDestination is Thing destinationAsThing && (nonSlotGroupThingOwner = destinationAsThing.TryGetInnerInteractableThingOwner()) != null)
            {
                storeTarget = new StoreTarget(destinationAsThing);
            }
            else
            {
                Log.Error("Don't know how to handle HaulToStorageJob for storage " + haulDestination.ToStringSafe() + ". thing=" + thing.ToStringSafe());
                return null;
            }
        }
        else
        {
            JobFailReason.Is("NoEmptyPlaceLower".Translate());
            return null;
        }

        //credit to Dingo
        var capacityStoreCell
            = storeTarget.container is null ? CapacityAt(thing, storeTarget.cell, destMap)
            : nonSlotGroupThingOwner.GetCountCanAccept(thing);

        if (capacityStoreCell == 0)
        {
            return HaulAIAcrossMapsUtility.HaulToStorageJob(pawn, thing, false, exitSpot, enterSpot);
        }

        var job = JobMaker.MakeJob(VMF_PUAH_DefOf.VMF_HaulToInventoryAcrossMaps, null, storeTarget);   //Things will be in queues
        Log.Message($"-------------------------------------------------------------------");
        Log.Message($"------------------------------------------------------------------");//different size so the log doesn't count it 2x
        Log.Message($"{pawn} job found to haul: {thing} to {storeTarget}:{capacityStoreCell}, looking for more now");

        //Find what fits in inventory, set nextThingLeftOverCount to be 
        var nextThingLeftOverCount = 0;
        var encumberance = MassUtility.EncumbrancePercent(pawn);
        job.targetQueueA = new List<LocalTargetInfo>(); //more things
        job.targetQueueB = new List<LocalTargetInfo>(); //more storage; keep in mind the job doesn't use it, but reserve it so you don't over-haul
        job.countQueue = new List<int>();//thing counts

        var ceOverweight = false;

        if (ModCompatibilityCheck.CombatExtendedIsActive)
        {
            ceOverweight = (bool)CompatHelperInvoker.CeOverweight(null, pawn);
        }

        var baseTargetPos = destMap.IsVehicleMapOf(out var vehicle) ? storeTarget.Position.ToBaseMapCoord(vehicle) : storeTarget.Position;
        var distanceToHaul = (baseTargetPos - thing.PositionOnBaseMap()).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION;
        var distanceToSearchMore = Math.Max(12f, distanceToHaul);

        //Find extra things than can be hauled to inventory, queue to reserve them
        var haulUrgentlyDesignation = PickUpAndHaulDesignationDefOf.haulUrgently;
        var isUrgent = ModCompatibilityCheck.AllowToolIsActive && designationManager.DesignationOn(thing)?.def == haulUrgentlyDesignation;

        var haulables = new List<Thing>(thingMap.listerHaulables.ThingsPotentiallyNeedingHauling());
        Comparer.rootCell = thing.PositionOnBaseMap();
        haulables.Sort(Comparer);

        var nextThing = thing;
        var lastThing = thing;

        var storeCellCapacity = new Dictionary<StoreTarget, CellAllocation>()
        {
            [storeTarget] = new CellAllocation(nextThing, capacityStoreCell)
        };
        //skipTargets = new() { storeTarget };
        skipCells = new HashSet<IntVec3>();
        skipThings = new HashSet<Thing>();
        if (storeTarget.container != null)
        {
            skipThings.Add(storeTarget.container);
        }
        else
        {
            skipCells.Add(storeTarget.cell);
        }

        bool Validator(Thing t)
            => (!isUrgent || designationManager.DesignationOn(t)?.def == haulUrgentlyDesignation)
            && GoodThingToHaul(t, pawn) && HaulAIAcrossMapsUtility.PawnCanAutomaticallyHaulFast(pawn, t, false, out _, out _); //forced is false, may differ from first thing

        haulables.Remove(thing);

        do
        {
            if (AllocateThingAtCell(storeCellCapacity, pawn, nextThing, job, destMap))
            {
                lastThing = nextThing;
                encumberance += AddedEncumberance(pawn, nextThing);

                if (encumberance > 1 || ceOverweight)
                {
                    //can't CountToPickUpUntilOverEncumbered here, pawn doesn't actually hold these things yet
                    nextThingLeftOverCount = CountPastCapacity(pawn, nextThing, encumberance);
                    Log.Message($"Inventory allocated, will carry {nextThing}:{nextThingLeftOverCount}");
                    break;
                }
            }
        }
        while ((nextThing = GetClosestAndRemove(lastThing.Position, thingMap, haulables, PathEndMode.ClosestTouch,
            TraverseParms.For(pawn), distanceToSearchMore, Validator)) != null);

        if (nextThing == null)
        {
            skipCells = null;
            skipThings = null;
            //skipTargets = null;
            return job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
        }

        //Find what can be carried
        //this doesn't actually get pickupandhauled, but will hold the reservation so others don't grab what this pawn can carry
        haulables.RemoveAll(t => !t.CanStackWith(nextThing));

        var carryCapacity = pawn.carryTracker.MaxStackSpaceEver(nextThing.def) - nextThingLeftOverCount;
        if (carryCapacity == 0)
        {
            Log.Message("Can't carry more, nevermind!");
            skipCells = null;
            skipThings = null;
            //skipTargets = null;
            return job;
        }
        Log.Message($"Looking for more like {nextThing}");

        while ((nextThing = GetClosestAndRemove(nextThing.Position, thingMap, haulables,
               PathEndMode.ClosestTouch, TraverseParms.For(pawn), 8f, Validator)) != null)
        {
            carryCapacity -= nextThing.stackCount;

            if (AllocateThingAtCell(storeCellCapacity, pawn, nextThing, job, destMap))
            {
                break;
            }

            if (carryCapacity <= 0)
            {
                var lastCount = job.countQueue.Pop() + carryCapacity;
                job.countQueue.Add(lastCount);
                Log.Message($"Nevermind, last count is {lastCount}");
                break;
            }
        }

        skipCells = null;
        skipThings = null;
        //skipTargets = null;
        return job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
    }

    private static bool HaulToHopperJob(Thing thing, IntVec3 targetCell, Map map)
    {
        if (thing.def.IsNutritionGivingIngestible
            && (thing.def.ingestible.preferability is FoodPreferability.RawBad || thing.def.ingestible.preferability is FoodPreferability.RawTasty))
        {
            var thingList = targetCell.GetThingList(map);
            for (var i = 0; i < thingList.Count; i++)
            {
                if (thingList[i].def == ThingDefOf.Hopper)
                {
                    return true;
                }
            }
        }
        return false;
    }

    new public static Thing GetClosestAndRemove(IntVec3 center, Map map, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
    {
        if (searchSet == null || !searchSet.Any())
        {
            return null;
        }

        float num = maxDistance * maxDistance;
        while (true)
        {
            Thing thing = FindClosestThing(searchSet, center, out int index);
            if (thing == null)
            {
                break;
            }

            searchSet.RemoveAt(index);
            if (thing.Spawned)
            {
                if ((center - thing.Position).LengthHorizontalSquared > num)
                {
                    break;
                }

                if (ReachabilityUtilityOnVehicle.CanReach(map, center, thing, peMode, traverseParams, map, out _, out _) && (validator == null || validator(thing)))
                {
                    return thing;
                }
            }
        }

        return null;
    }

    public static bool Stackable(Thing nextThing, KeyValuePair<StoreTarget, CellAllocation> allocation, Map destMap)
    {
        if (nextThing != allocation.Value.allocated && !allocation.Value.allocated.CanStackWith(nextThing))
        {
            return HoldMultipleThings_Support.StackableAt(nextThing, allocation.Key.cell, destMap);
        }

        return true;
    }

    public static bool AllocateThingAtCell(Dictionary<StoreTarget, CellAllocation> storeCellCapacity, Pawn pawn, Thing nextThing, Job job, Map destMap)
    {
        var map = pawn.Map;
        var thingMap = nextThing.MapHeld;
        var allocation = storeCellCapacity.FirstOrDefault(kvp =>
            kvp.Key is var storeTarget
            && (storeTarget.container?.TryGetInnerInteractableThingOwner().CanAcceptAnyOf(nextThing)
            ?? storeTarget.cell.GetSlotGroup(destMap).parent.Accepts(nextThing))
            && Stackable(nextThing, kvp, destMap));
        var storeCell = allocation.Key;

        //Can't stack with allocated cells, find a new cell:
        if (storeCell == default)
        {
            var currentPriority = StoreUtility.CurrentStoragePriorityOf(nextThing);
            if (TryFindBestBetterStorageFor(nextThing, pawn, destMap, currentPriority, pawn.Faction, out var nextStoreCell, out var haulDestination, out var innerInteractableThingOwner))
            {
                if (innerInteractableThingOwner is null)
                {
                    storeCell = new StoreTarget(nextStoreCell);
                    job.targetQueueB.Add(nextStoreCell);

                    storeCellCapacity[storeCell] = new CellAllocation(nextThing, CapacityAt(nextThing, nextStoreCell, destMap));

                    Log.Message($"New cell for unstackable {nextThing} = {nextStoreCell}");
                }
                else
                {
                    var destinationAsThing = (Thing)haulDestination;
                    storeCell = new StoreTarget(destinationAsThing);
                    job.targetQueueB.Add(destinationAsThing);

                    storeCellCapacity[storeCell] = new CellAllocation(nextThing, innerInteractableThingOwner.GetCountCanAccept(nextThing));

                    Log.Message($"New haulDestination for unstackable {nextThing} = {haulDestination}");
                }
            }
            else
            {
                Log.Message($"{nextThing} can't stack with allocated cells");

                if (job.targetQueueA.NullOrEmpty())
                {
                    job.targetQueueA.Add(nextThing);
                }

                return false;
            }
        }

        job.targetQueueA.Add(nextThing);
        var count = nextThing.stackCount;
        storeCellCapacity[storeCell].capacity -= count;
        Log.Message($"{pawn} allocating {nextThing}:{count}, now {storeCell}:{storeCellCapacity[storeCell].capacity}");

        while (storeCellCapacity[storeCell].capacity <= 0)
        {
            var capacityOver = -storeCellCapacity[storeCell].capacity;
            storeCellCapacity.Remove(storeCell);

            Log.Message($"{pawn} overdone {storeCell} by {capacityOver}");

            if (capacityOver == 0)
            {
                break;  //don't find new cell, might not have more of this thing to haul
            }

            var currentPriority = StoreUtility.CurrentStoragePriorityOf(nextThing);
            if (TryFindBestBetterStorageFor(nextThing, pawn, destMap, currentPriority, pawn.Faction, out var nextStoreCell, out var nextHaulDestination, out var innerInteractableThingOwner))
            {
                if (innerInteractableThingOwner is null)
                {
                    storeCell = new StoreTarget(nextStoreCell);
                    job.targetQueueB.Add(nextStoreCell);

                    var capacity = CapacityAt(nextThing, nextStoreCell, destMap) - capacityOver;
                    storeCellCapacity[storeCell] = new CellAllocation(nextThing, capacity);

                    Log.Message($"New cell {nextStoreCell}:{capacity}, allocated extra {capacityOver}");
                }
                else
                {
                    var destinationAsThing = (Thing)nextHaulDestination;
                    storeCell = new StoreTarget(destinationAsThing);
                    job.targetQueueB.Add(destinationAsThing);

                    var capacity = innerInteractableThingOwner.GetCountCanAccept(nextThing) - capacityOver;

                    storeCellCapacity[storeCell] = new CellAllocation(nextThing, capacity);

                    Log.Message($"New haulDestination {nextHaulDestination}:{capacity}, allocated extra {capacityOver}");
                }
            }
            else
            {
                count -= capacityOver;
                job.countQueue.Add(count);
                Log.Message($"Nowhere else to store, allocated {nextThing}:{count}");
                return false;
            }
        }
        job.countQueue.Add(count);
        Log.Message($"{nextThing}:{count} allocated");
        return true;
    }

    new public static bool TryFindBestBetterStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, out IHaulDestination haulDestination, out ThingOwner innerInteractableThingOwner)
    {
        var storagePriority = StoragePriority.Unstored;
        innerInteractableThingOwner = null;
        if (TryFindBestBetterStoreCellFor(t, carrier, map, currentPriority, faction, out var foundCell2))
        {
            storagePriority = foundCell2.GetSlotGroup(map).Settings.Priority;
        }

        if (!TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, currentPriority, faction, out var haulDestination2, false))
        {
            haulDestination2 = null;
        }

        if (storagePriority == StoragePriority.Unstored && haulDestination2 == null)
        {
            foundCell = IntVec3.Invalid;
            haulDestination = null;
            return false;
        }

        if (haulDestination2 != null && (storagePriority == StoragePriority.Unstored || (int)haulDestination2.GetStoreSettings().Priority > (int)storagePriority))
        {
            foundCell = IntVec3.Invalid;
            haulDestination = haulDestination2;

            if (!(haulDestination2 is Thing destinationAsThing))
            {
                Log.Error($"{haulDestination2} is not a valid Thing. Pick Up And Haul can't work with this");
            }
            else
            {
                innerInteractableThingOwner = destinationAsThing.TryGetInnerInteractableThingOwner();
            }

            if (innerInteractableThingOwner is null)
            {
                Log.Error($"{haulDestination2} gave null ThingOwner during lookup in Pick Up And Haul's WorkGiver_HaulToInventory");
            }

            return true;
        }

        foundCell = foundCell2;
        haulDestination = foundCell2.GetSlotGroup(map).parent;
        return true;
    }

    new public static bool TryFindBestBetterStoreCellFor(Thing thing, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell)
    {
        var haulDestinations = map.haulDestinationManager.AllGroupsListInPriorityOrder;
        for (var i = 0; i < haulDestinations.Count; i++)
        {
            var slotGroup = haulDestinations[i];
            if (slotGroup.Settings.Priority <= currentPriority || !slotGroup.parent.Accepts(thing))
            {
                continue;
            }

            var cellsList = slotGroup.CellsList;

            for (var j = 0; j < cellsList.Count; j++)
            {
                var cell = cellsList[j];
                if (skipCells.Contains(cell))
                {
                    continue;
                }

                if (StoreAcrossMapsUtility.IsGoodStoreCell(cell, map, thing, carrier, faction, out _, out _) && cell != default)
                {
                    foundCell = cell;

                    skipCells.Add(cell);

                    return true;
                }
            }
        }
        foundCell = IntVec3.Invalid;
        return false;
    }

    new public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IHaulDestination haulDestination, bool acceptSamePriority)
    {
        var allHaulDestinationsListInPriorityOrder = map.haulDestinationManager.AllHaulDestinationsListInPriorityOrder;
        var intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
        var num = float.MaxValue;
        var storagePriority = StoragePriority.Unstored;
        haulDestination = null;
        for (var i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
        {
            var iHaulDestination = allHaulDestinationsListInPriorityOrder[i];

            if (iHaulDestination is ISlotGroupParent || (iHaulDestination is Building_Grave && !t.CanBeBuried()))
            {
                continue;
            }

            var priority = iHaulDestination.GetStoreSettings().Priority;
            if ((int)priority < (int)storagePriority || (acceptSamePriority && (int)priority < (int)currentPriority) || (!acceptSamePriority && (int)priority <= (int)currentPriority))
            {
                break;
            }

            float num2 = intVec.DistanceToSquared(iHaulDestination.Position);
            if (num2 > num || !iHaulDestination.Accepts(t))
            {
                continue;
            }

            if (iHaulDestination is Thing thing)
            {
                if (skipThings.Contains(thing) || thing.Faction != faction)
                {
                    continue;
                }

                if (carrier != null)
                {
                    if (thing.IsForbidden(carrier)
                        || !carrier.CanReserveNew(thing)
                        || !ReachabilityUtilityOnVehicle.CanReach(map, intVec, thing, PathEndMode.ClosestTouch, TraverseParms.For(carrier), thing.MapHeld, out _, out _))
                    {
                        continue;
                    }
                }
                else if (faction != null)
                {
                    if (thing.IsForbidden(faction) || thing.MapHeld.reservationManager.IsReservedByAnyoneOf(thing, faction))
                    {
                        continue;
                    }
                }

                skipThings.Add(thing);
            }
            else
            {
                //not supported. Seems dumb
                continue;

                //if (carrier != null && !carrier.Map.reachability.CanReach(intVec, iHaulDestination.Position, PathEndMode.ClosestTouch, TraverseParms.For(carrier)))
                //	continue;
            }

            num = num2;
            storagePriority = priority;
            haulDestination = iHaulDestination;
        }

        return haulDestination != null;
    }
}
