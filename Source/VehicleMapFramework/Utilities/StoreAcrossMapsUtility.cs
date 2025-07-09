using RimWorld;
using SmashTools;
using System.Collections.Generic;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class StoreAcrossMapsUtility
{
    public static Map tmpDestMap;

    public static bool TryFindBestBetterStoreCellFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IntVec3 foundCell, bool needAccurateResult)
    {
        tmpDestMap = null;
        List<SlotGroup> allGroupsListInPriorityOrder = map.BaseMap().GetCachedMapComponent<CrossMapHaulDestinationManager>().AllGroupsListInPriorityOrder;

        if (allGroupsListInPriorityOrder.Count == 0)
        {
            return false;
        }
        StoragePriority storagePriority = currentPriority;
        float num = int.MaxValue;
        IntVec3 invalid = IntVec3.Invalid;
        for (int i = 0; i < allGroupsListInPriorityOrder.Count; i++)
        {
            SlotGroup slotGroup = allGroupsListInPriorityOrder[i];
            Map storeMap = slotGroup.parent?.Map;
            if (storeMap is null || map == storeMap)
            {
                continue;
            }

            StoragePriority priority = slotGroup.Settings.Priority;
            if (priority < storagePriority || priority <= currentPriority)
            {
                break;
            }
            TryFindBestBetterStoreCellForWorker(t, carrier, storeMap, faction, slotGroup, needAccurateResult, ref invalid, ref num, ref storagePriority);
        }
        if (!invalid.IsValid)
        {
            return false;
        }
        foundCell = invalid;
        return true;
    }

    public static void TryFindBestBetterStoreCellForWorker(Thing t, Pawn carrier, Map map, Faction faction, ISlotGroup slotGroup, bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared, ref StoragePriority foundPriority)
    {
        if (slotGroup == null)
        {
            return;
        }
        if (!slotGroup.Settings.AllowedToAccept(t))
        {
            return;
        }
        IntVec3 a = t.SpawnedOrAnyParentSpawned ? t.PositionHeldOnBaseMap().CellOnAnotherMap(map) : carrier.PositionHeldOnBaseMap().CellOnAnotherMap(map);
        List<IntVec3> cellsList = slotGroup.CellsList;
        int count = cellsList.Count;
        int num;
        if (needAccurateResult)
        {
            num = Mathf.FloorToInt(count * Rand.Range(0.005f, 0.018f));
        }
        else
        {
            num = 0;
        }
        for (int i = 0; i < count; i++)
        {
            IntVec3 intVec = cellsList[i];
            float num2 = (a - intVec).LengthHorizontalSquared;
            if (num2 <= closestDistSquared && IsGoodStoreCell(intVec, map, t, carrier, faction))
            {
                closestSlot = intVec;
                closestDistSquared = num2;
                foundPriority = slotGroup.Settings.Priority;
                tmpDestMap = map;
                if (i >= num)
                {
                    break;
                }
            }
        }
    }

    public static bool IsGoodStoreCell(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction)
    {
        if (carrier != null)
        {
            try
            {
                Patch_ForbidUtility_IsForbidden.Map = map;
                if (c.IsForbidden(carrier))
                {
                    return false;
                }
            }
            finally
            {
                Patch_ForbidUtility_IsForbidden.Map = null;
            }

        }
        if (!c.IsValidStorageFor(map, t))
        {
            return false;
        }
        if (carrier != null)
        {
            if (!carrier.CanReserveNew(c, map))
            {
                return false;
            }
        }
        else if (faction != null && map.reservationManager.IsReservedByAnyoneOf(c, faction))
        {
            return false;
        }
        if (c.ContainsStaticFire(map))
        {
            return false;
        }
        List<Thing> thingList = c.GetThingList(map);
        for (int i = 0; i < thingList.Count; i++)
        {
            if (thingList[i] is IConstructible && GenConstruct.BlocksConstruction(thingList[i], t))
            {
                return false;
            }
        }
        if (carrier != null)
        {
            Thing spawnedParentOrMe;
            IntVec3 start;
            Map startMap;
            if ((spawnedParentOrMe = t.SpawnedParentOrMe) != null)
            {
                startMap = spawnedParentOrMe.Map;
                if (spawnedParentOrMe != t && spawnedParentOrMe.def.hasInteractionCell)
                {
                    start = spawnedParentOrMe.InteractionCell;
                }
                else
                {
                    start = spawnedParentOrMe.Position;
                }
            }
            else
            {
                startMap = carrier.Map;
                start = carrier.PositionHeld;
            }
            if (!CrossMapReachabilityUtility.CanReach(startMap, start, c, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false), map, out _, out _))
            {
                return false;
            }
        }
        return true;
    }

    private static bool NoStorageBlockersIn(IntVec3 c, Map map, Thing thing)
    {
        List<Thing> list = map.thingGrid.ThingsListAt(c);
        bool flag = false;
        for (int i = 0; i < list.Count; i++)
        {
            Thing thing2 = list[i];
            if (!flag && thing2.def.EverStorable(false) && thing2.CanStackWith(thing) && thing2.stackCount < thing2.def.stackLimit)
            {
                flag = true;
            }
            if (thing2.def.entityDefToBuild != null && thing2.def.entityDefToBuild.passability != Traversability.Standable)
            {
                return false;
            }
            if (thing2.def.surfaceType == SurfaceType.None && thing2.def.passability != Traversability.Standable && (c.GetMaxItemsAllowedInCell(map) <= 1 || thing2.def.category != ThingCategory.Item))
            {
                return false;
            }
        }
        return flag || c.GetItemCount(map) < c.GetMaxItemsAllowedInCell(map);
    }

    public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation)
    {
        List<IHaulDestination> allHaulDestinationsListInPriorityOrder = map.BaseMap().GetCachedMapComponent<CrossMapHaulDestinationManager>().AllHaulDestinationsListInPriorityOrder;

        Map thingMap = t.SpawnedOrAnyParentSpawned ? t.MapHeld : carrier.MapHeld;
        IntVec3 intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
        IntVec3 intVecOnBase = t.SpawnedOrAnyParentSpawned ? t.PositionHeldOnBaseMap() : carrier.PositionHeldOnBaseMap();
        float num = float.MaxValue;
        StoragePriority storagePriority = StoragePriority.Unstored;
        for (int i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
        {
            var destMap = allHaulDestinationsListInPriorityOrder[i].Map;
            if (destMap is null || destMap == map)
            {
                continue;
            }

            if (allHaulDestinationsListInPriorityOrder[i] is not ISlotGroupParent && (allHaulDestinationsListInPriorityOrder[i] is not Building_Grave || t.CanBeBuried()))
            {
                StoragePriority priority = allHaulDestinationsListInPriorityOrder[i].GetStoreSettings().Priority;
                if (priority < storagePriority || (acceptSamePriority && priority < currentPriority) || (!acceptSamePriority && priority <= currentPriority))
                {
                    break;
                }
                float num2 = intVecOnBase.DistanceToSquared(allHaulDestinationsListInPriorityOrder[i].PositionOnBaseMap());
                if (num2 <= num && allHaulDestinationsListInPriorityOrder[i].Accepts(t))
                {
                    Thing thing = allHaulDestinationsListInPriorityOrder[i] as Thing;
                    if (thing == null || thing.Faction == faction)
                    {
                        if (thing != null)
                        {
                            if (carrier != null)
                            {
                                if (thing.IsForbidden(carrier))
                                {
                                    continue;
                                }
                            }
                            else if (faction != null && thing.IsForbidden(faction))
                            {
                                continue;
                            }
                        }
                        if (thing != null && requiresDestReservation)
                        {
                            if (thing is IHaulEnroute enroute)
                            {
                                if (!thingMap.reservationManager.OnlyReservationsForJobDef(thing, JobDefOf.HaulToContainer, false))
                                {
                                    continue;
                                }
                                if (enroute.GetSpaceRemainingWithEnroute(t.def, null) <= 0)
                                {
                                    continue;
                                }
                            }
                            else if (carrier != null)
                            {
                                if (!carrier.CanReserveNew(thing, thingMap))
                                {
                                    continue;
                                }
                            }
                            else if (faction != null && thingMap.reservationManager.IsReservedByAnyoneOf(thing, faction))
                            {
                                continue;
                            }
                        }
                        if (carrier != null)
                        {
                            if (thing != null)
                            {
                                if (!CrossMapReachabilityUtility.CanReach(thingMap, intVec, thing, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false), thing.Map))
                                {
                                    continue;
                                }
                            }
                            else if (!CrossMapReachabilityUtility.CanReach(thingMap, intVec, allHaulDestinationsListInPriorityOrder[i].Position, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false), allHaulDestinationsListInPriorityOrder[i].Map))
                            {
                                continue;
                            }
                        }
                        num = num2;
                        storagePriority = priority;
                        haulDestination = allHaulDestinationsListInPriorityOrder[i];

                    }
                }
            }
        }
        return haulDestination != null;
    }
}