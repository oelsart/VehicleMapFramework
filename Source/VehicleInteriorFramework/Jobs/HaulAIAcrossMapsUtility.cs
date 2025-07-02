using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors;

public static class HaulAIAcrossMapsUtility
{
    public static bool PawnCanAutomaticallyHaulFastReplace(Pawn p, Thing t, bool forced)
    {
        return PawnCanAutomaticallyHaulFast(p, t, forced, out _, out _);
    }

    public static bool PawnCanAutomaticallyHaulFast(Pawn p, Thing t, bool forced, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        Building building;
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (t is UnfinishedThing unfinishedThing && unfinishedThing.BoundBill != null && ((building = unfinishedThing.BoundBill.billStack.billGiver as Building) == null || (building.Spawned && building.OccupiedRect().ExpandedBy(1).Contains(unfinishedThing.Position))))
        {
            return false;
        }
        if (!p.CanReach(t, PathEndMode.ClosestTouch, p.NormalMaxDanger(), false, false, TraverseMode.ByPawn, t.MapHeld, out exitSpot, out enterSpot))
        {
            return false;
        }
        if (!p.CanReserve(t, t.MapHeld, 1, -1, null, forced))
        {
            return false;
        }
        if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        {
            return false;
        }
        if (t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.IsSociallyProper(p, false, true))
        {
            JobFailReason.Is("ReservedForPrisoners".Translate(), null);
            return false;
        }
        if (t.IsBurning())
        {
            JobFailReason.Is("BurningLower".Translate(), null);
            return false;
        }
        return true;
    }

    public static Job HaulToStorageJobReplace(Pawn p, Thing t, bool forced)
    {
        return HaulToStorageJob(p, t, forced, TargetInfo.Invalid, TargetInfo.Invalid);
    }

    public static Job HaulToStorageJob(Pawn p, Thing t, bool forced, TargetInfo exitSpot, TargetInfo enterSpot)
    {
        StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(t, forced);
        if (!StoreAcrossMapsUtility.TryFindBestBetterStorageFor(t, p, t.Map, currentPriority, p.Faction, out IntVec3 storeCell, out IHaulDestination haulDestination, true, out var exitSpot2, out var enterSpot2))
        {
            JobFailReason.Is(HaulAIUtility.NoEmptyPlaceLowerTrans, null);
            return null;
        }
        if (haulDestination is ISlotGroupParent)
        {
            return HaulAIAcrossMapsUtility.HaulToCellStorageJob(p, t, storeCell, false, exitSpot, enterSpot, exitSpot2, enterSpot2);
        }
        if (haulDestination is Thing thing && thing.TryGetInnerInteractableThingOwner() != null)
        {
            return HaulAIAcrossMapsUtility.HaulToContainerJob(p, t, thing, exitSpot, enterSpot, exitSpot2, enterSpot2);
        }
        Log.Error("Don't know how to handle HaulToStorageJob for storage " + haulDestination.ToStringSafe<IHaulDestination>() + ". thing=" + t.ToStringSafe<Thing>());
        return null;
    }

    public static Job HaulToCellStorageJob(Pawn p, Thing t, IntVec3 storeCell, bool fitInStoreCell, TargetInfo exitSpot, TargetInfo enterSpot, TargetInfo exitSpot2, TargetInfo enterSpot2)
    {
        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToCellAcrossMaps, t, storeCell);
        Map destMap = enterSpot2.Map ?? exitSpot2.Map?.BaseMap() ?? enterSpot.Map ?? exitSpot.Map?.BaseMap() ?? p.Map;
        ISlotGroup slotGroup = destMap.haulDestinationManager.SlotGroupAt(storeCell);
        ISlotGroup storageGroup = slotGroup.StorageGroup;
        ISlotGroup slotGroup2 = storageGroup ?? slotGroup;
        if (destMap.thingGrid.ThingAt(storeCell, t.def) != null)
        {
            if (fitInStoreCell)
            {
                job.count = storeCell.GetItemStackSpaceLeftFor(p.Map, t.def);
            }
            else
            {
                job.count = t.def.stackLimit;
            }
        }
        else
        {
            job.count = 99999;
        }
        int num = 0;
        float statValue = p.GetStatValue(StatDefOf.CarryingCapacity, true, -1);
        List<IntVec3> cellsList = slotGroup2.CellsList;
        for (int i = 0; i < cellsList.Count; i++)
        {
            if (StoreAcrossMapsUtility.IsGoodStoreCell(cellsList[i], destMap, t, p, p.Faction, out _, out var _))
            {
                num += cellsList[i].GetItemStackSpaceLeftFor(destMap, t.def);
                if (num >= job.count || num >= statValue)
                {
                    break;
                }
            }
        }
        job.count = Mathf.Min(job.count, num);
        job.haulOpportunisticDuplicates = true;
        job.haulMode = HaulMode.ToCellStorage;
        job.SetSpotsToJobAcrossMaps(p, exitSpot, enterSpot, exitSpot2, enterSpot2);
        return job;
    }

    public static Job HaulToContainerJob(Pawn p, Thing t, Thing container, TargetInfo exitSpot, TargetInfo enterSpot, TargetInfo exitSpot2, TargetInfo enterSpot2)
    {
        ThingOwner thingOwner = container.TryGetInnerInteractableThingOwner();
        if (thingOwner == null)
        {
            Log.Error(container.ToStringSafe<Thing>() + " gave null ThingOwner.");
            return null;
        }
        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToContainerAcrossMaps, t, container);
        job.count = Mathf.Min(t.stackCount, thingOwner.GetCountCanAccept(t, true));
        job.haulMode = HaulMode.ToContainer;
        job.SetSpotsToJobAcrossMaps(p, exitSpot, enterSpot, exitSpot2, enterSpot2);
        return job;
    }

    public static bool CanHaulAside(Pawn p, Thing t, out IntVec3 storeCell, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        storeCell = IntVec3.Invalid;
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (!t.def.EverHaulable)
        {
            return false;
        }

        if (t.IsBurning())
        {
            return false;
        }

        if (!p.CanReserveAndReach(t.Map, t, PathEndMode.ClosestTouch, p.NormalMaxDanger(), 1, -1, null, false, out exitSpot, out enterSpot))
        {
            return false;
        }

        if (!TryFindSpotToPlaceHaulableCloseTo(t, p, t.PositionHeld, out storeCell))
        {
            return false;
        }

        return true;
    }

    public static Job HaulAsideJobFor(Pawn p, Thing t)
    {
        if (!CanHaulAside(p, t, out var storeCell, out var exitSpot, out var enterSpot))
        {
            return null;
        }

        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToCellAcrossMaps, t, storeCell).SetSpotsToJobAcrossMaps(p, exitSpot, enterSpot);
        job.count = 99999;
        job.haulOpportunisticDuplicates = false;
        job.haulMode = HaulMode.ToCellNonStorage;
        job.ignoreDesignations = true;
        return job;
    }

    private static bool TryFindSpotToPlaceHaulableCloseTo(Thing haulable, Pawn worker, IntVec3 center, out IntVec3 spot)
    {
        Region region = center.GetRegion(haulable.Map);
        if (region == null)
        {
            spot = center;
            return false;
        }

        TraverseParms traverseParms = TraverseParms.For(worker);
        IntVec3 foundCell = IntVec3.Invalid;
        RegionTraverser.BreadthFirstTraverse(region, (from, r) => r.Allows(traverseParms, isDestination: false), delegate (Region r)
        {
            candidates.Clear();
            candidates.AddRange(r.Cells);
            candidates.Sort((a, b) => a.DistanceToSquared(center).CompareTo(b.DistanceToSquared(center)));
            for (int i = 0; i < candidates.Count; i++)
            {
                IntVec3 intVec = candidates[i];
                if (HaulablePlaceValidator(haulable, worker, intVec))
                {
                    foundCell = intVec;
                    return true;
                }
            }

            return false;
        }, 100);
        if (foundCell.IsValid)
        {
            spot = foundCell;
            return true;
        }

        spot = center;
        return false;
    }

    private static bool HaulablePlaceValidator(Thing haulable, Pawn worker, IntVec3 c)
    {
        if (!haulable.Map.reservationManager.CanReserve(worker, c) || !haulable.Map.reachability.CanReach(haulable.Position, c, PathEndMode.Touch, TraverseParms.For(worker)))
        {
            return false;
        }

        if (GenPlace.HaulPlaceBlockerIn(haulable, c, haulable.Map, checkBlueprintsAndFrames: true) != null)
        {
            return false;
        }

        if (!c.Standable(haulable.Map))
        {
            return false;
        }

        if (c == haulable.Position && haulable.Spawned)
        {
            return false;
        }

        if (c.ContainsStaticFire(haulable.Map))
        {
            return false;
        }

        if (haulable != null && haulable.def.BlocksPlanting() && haulable.Map.zoneManager.ZoneAt(c) is Zone_Growing)
        {
            return false;
        }

        if (haulable.def.passability != 0)
        {
            for (int i = 0; i < 8; i++)
            {
                IntVec3 c2 = c + GenAdj.AdjacentCells[i];
                if (haulable.Map.designationManager.DesignationAt(c2, DesignationDefOf.Mine) != null || haulable.Map.designationManager.DesignationAt(c2, DesignationDefOf.MineVein) != null)
                {
                    return false;
                }
            }
        }

        Building edifice = c.GetEdifice(haulable.Map);
        if (edifice != null && edifice is Building_Trap)
        {
            return false;
        }

        if (haulable is UnfinishedThing unfinishedThing && unfinishedThing.BoundWorkTable != null)
        {
            if (unfinishedThing.BoundWorkTable.InteractionCell == c)
            {
                return false;
            }

            List<Thing> thingList = c.GetThingList(haulable.Map);
            for (int j = 0; j < thingList.Count; j++)
            {
                if (unfinishedThing.BoundWorkTable == thingList[j])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static readonly List<IntVec3> candidates = [];
}
