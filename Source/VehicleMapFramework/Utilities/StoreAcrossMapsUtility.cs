using RimWorld;
using SmashTools;
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
        var allGroupsListInPriorityOrder = map.BaseMap().GetCachedMapComponent<CrossMapHaulDestinationManager>().AllGroupsListInPriorityOrder;

        if (allGroupsListInPriorityOrder.Count == 0)
        {
            return false;
        }
        var storagePriority = currentPriority;
        float num = int.MaxValue;
        var invalid = IntVec3.Invalid;
        for (var i = 0; i < allGroupsListInPriorityOrder.Count; i++)
        {
            var slotGroup = allGroupsListInPriorityOrder[i];
            var storeMap = slotGroup.parent?.Map;
            if (storeMap is null || map == storeMap)
            {
                continue;
            }

            var priority = slotGroup.Settings.Priority;
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
        var a = t.SpawnedOrAnyParentSpawned ? t.PositionHeldOnBaseMap().CellOnAnotherMap(map) : carrier.PositionHeldOnBaseMap().CellOnAnotherMap(map);
        var cellsList = slotGroup.CellsList;
        var count = cellsList.Count;
        int num;
        if (needAccurateResult)
        {
            num = Mathf.FloorToInt(count * Rand.Range(0.005f, 0.018f));
        }
        else
        {
            num = 0;
        }
        for (var i = 0; i < count; i++)
        {
            var intVec = cellsList[i];
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
        var thingList = c.GetThingList(map);
        for (var i = 0; i < thingList.Count; i++)
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
            if (!CrossMapReachabilityUtility.CanReach(startMap, start, c, PathEndMode.ClosestTouch, TraverseParms.For(carrier), map, out _, out _))
            {
                return false;
            }
        }
        return true;
    }

    public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, ref IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation)
    {
        var allHaulDestinationsListInPriorityOrder = map.BaseMap().GetCachedMapComponent<CrossMapHaulDestinationManager>().AllHaulDestinationsListInPriorityOrder;

        var thingMap = t.SpawnedOrAnyParentSpawned ? t.MapHeld : carrier.MapHeld;
        var intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
        var intVecOnBase = t.SpawnedOrAnyParentSpawned ? t.PositionHeldOnBaseMap() : carrier.PositionHeldOnBaseMap();
        var num = float.MaxValue;
        var storagePriority = StoragePriority.Unstored;
        for (var i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
        {
            var destMap = allHaulDestinationsListInPriorityOrder[i].Map;
            if (destMap is null || destMap == map)
            {
                continue;
            }

            if (allHaulDestinationsListInPriorityOrder[i] is not ISlotGroupParent && (allHaulDestinationsListInPriorityOrder[i] is not Building_Grave || t.CanBeBuried()))
            {
                var priority = allHaulDestinationsListInPriorityOrder[i].GetStoreSettings().Priority;
                if (priority < storagePriority || (acceptSamePriority && priority < currentPriority) || (!acceptSamePriority && priority <= currentPriority))
                {
                    break;
                }
                float num2 = intVecOnBase.DistanceToSquared(allHaulDestinationsListInPriorityOrder[i].PositionOnBaseMap());
                if (num2 <= num && allHaulDestinationsListInPriorityOrder[i].Accepts(t))
                {
                    var thing = allHaulDestinationsListInPriorityOrder[i] as Thing;
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
                                if (!thingMap.reservationManager.OnlyReservationsForJobDef(thing, JobDefOf.HaulToContainer))
                                {
                                    continue;
                                }
                                if (enroute.GetSpaceRemainingWithEnroute(t.def) <= 0)
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
                                if (!CrossMapReachabilityUtility.CanReach(thingMap, intVec, thing, PathEndMode.ClosestTouch, TraverseParms.For(carrier), thing.Map))
                                {
                                    continue;
                                }
                            }
                            else if (!CrossMapReachabilityUtility.CanReach(thingMap, intVec, allHaulDestinationsListInPriorityOrder[i].Position, PathEndMode.ClosestTouch, TraverseParms.For(carrier), allHaulDestinationsListInPriorityOrder[i].Map))
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