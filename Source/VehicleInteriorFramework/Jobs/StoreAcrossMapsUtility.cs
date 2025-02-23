using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class StoreAcrossMapsUtility
    {
        public static bool TryFindBestBetterStoreCellFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, bool needAccurateResult, out TargetInfo exitSpot, out TargetInfo enterSpot, out Map destMap)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            destMap = map;
            List<SlotGroup> allGroupsListInPriorityOrder = map.BaseMapAndVehicleMaps().SelectMany(m => m.haulDestinationManager.AllGroupsListInPriorityOrder).OrderByDescending(d => d.Settings.Priority).ToList();
            if (allGroupsListInPriorityOrder.Count == 0)
            {
                foundCell = IntVec3.Invalid;
                return false;
            }
            StoragePriority storagePriority = currentPriority;
            float num = 2.1474836E+09f;
            IntVec3 invalid = IntVec3.Invalid;
            int count = allGroupsListInPriorityOrder.Count;
            for (int i = 0; i < count; i++)
            {
                SlotGroup slotGroup = allGroupsListInPriorityOrder[i];
                StoragePriority priority = slotGroup.Settings.Priority;
                if (priority < storagePriority || priority <= currentPriority)
                {
                    break;
                }
                StoreAcrossMapsUtility.TryFindBestBetterStoreCellForWorker(t, carrier, slotGroup.parent.Map, faction, slotGroup, needAccurateResult, ref invalid, ref num, ref storagePriority, ref exitSpot, ref enterSpot, ref destMap);
            }
            if (!invalid.IsValid)
            {
                foundCell = IntVec3.Invalid;
                return false;
            }
            foundCell = invalid;
            return true;
        }

        private static void TryFindBestBetterStoreCellForWorker(Thing t, Pawn carrier, Map map, Faction faction, ISlotGroup slotGroup, bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared, ref StoragePriority foundPriority, ref TargetInfo exitSpot, ref TargetInfo enterSpot, ref Map destMap)
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
                num = Mathf.FloorToInt((float)count * Rand.Range(0.005f, 0.018f));
            }
            else
            {
                num = 0;
            }
            for (int i = 0; i < count; i++)
            {
                IntVec3 intVec = cellsList[i];
                float num2 = (float)(a - intVec).LengthHorizontalSquared;
                if (num2 <= closestDistSquared && StoreAcrossMapsUtility.IsGoodStoreCell(intVec, map, t, carrier, faction, out var exitSpot2, out var enterSpot2))
                {
                    exitSpot = exitSpot2;
                    enterSpot = enterSpot2;
                    closestSlot = intVec;
                    closestDistSquared = num2;
                    foundPriority = slotGroup.Settings.Priority;
                    destMap = map;
                    if (i >= num)
                    {
                        break;
                    }
                }
            }
        }

        public static bool IsGoodStoreCell(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction, out TargetInfo dest1, out TargetInfo dest2)
        {
            dest1 = TargetInfo.Invalid;
            dest2 = TargetInfo.Invalid;
            if (carrier != null && c.IsForbidden(carrier))
            {
                return false;
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
                if (!ReachabilityUtilityOnVehicle.CanReach(startMap, start, c, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false), map, out dest1, out dest2))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsForbidden(this IntVec3 c, Pawn pawn)
        {
            var cellOnBaseMap = c.CellOnAnotherMap(pawn.BaseMap());
            return ForbidUtility.CaresAboutForbidden(pawn, true, false) && (!cellOnBaseMap.InAllowedArea(pawn) || (pawn.mindState.maxDistToSquadFlag > 0f && !cellOnBaseMap.InHorDistOf(pawn.DutyLocation().CellOnAnotherMap(pawn.BaseMap()), pawn.mindState.maxDistToSquadFlag)));
        }

        public static bool InAllowedArea(this IntVec3 c, Pawn forPawn)
        {
            if (forPawn.playerSettings != null)
            {
                Area effectiveAreaRestrictionInPawnBaseMap = forPawn.EffectiveAreaRestrictionInPawnBaseMap();
                if (effectiveAreaRestrictionInPawnBaseMap != null && effectiveAreaRestrictionInPawnBaseMap.TrueCount > 0 && !effectiveAreaRestrictionInPawnBaseMap[c])
                {
                    return false;
                }
            }
            return true;
        }

        public static Area EffectiveAreaRestrictionInPawnBaseMap(this Pawn pawn)
        {
            if (!pawn.playerSettings.RespectsAllowedArea)
            {
                return null;
            }
            Area result;
            if (!allowedAreas(pawn.playerSettings).TryGetValue(pawn.MapHeld.BaseMap(), out result))
            {
                return null;
            }
            return result;
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

        public static bool TryFindBestBetterStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, out IHaulDestination haulDestination, bool needAccurateResult)
        {
            return StoreAcrossMapsUtility.TryFindBestBetterStorageFor(t, carrier, map, currentPriority, faction, out foundCell, out haulDestination, needAccurateResult, out _, out _);
        }

        public static bool TryFindBestBetterStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, out IHaulDestination haulDestination, bool needAccurateResult, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            StoragePriority storagePriority = StoragePriority.Unstored;
            if (StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(t, carrier, map, currentPriority, faction, out IntVec3 invalid, needAccurateResult, out TargetInfo exitSpot2, out TargetInfo enterSpot2, out var map2))
            {
                storagePriority = invalid.GetSlotGroup(map2)?.Settings.Priority ?? StoragePriority.Unstored;
            }
            if (!StoreAcrossMapsUtility.TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, currentPriority, faction, out IHaulDestination haulDestination2, false, true, out TargetInfo exitSpot3, out TargetInfo enterSpot3))
            {
                haulDestination2 = null;
            }
            if (storagePriority == StoragePriority.Unstored && haulDestination2 == null)
            {
                foundCell = IntVec3.Invalid;
                haulDestination = null;
                exitSpot = TargetInfo.Invalid;
                enterSpot = TargetInfo.Invalid;
                return false;
            }
            if (haulDestination2 != null && (storagePriority == StoragePriority.Unstored || haulDestination2.GetStoreSettings().Priority > storagePriority))
            {
                foundCell = IntVec3.Invalid;
                haulDestination = haulDestination2;
                exitSpot = exitSpot3;
                enterSpot = enterSpot3;
                return true;
            }
            foundCell = invalid;
            haulDestination = invalid.GetSlotGroup(map2)?.parent;
            exitSpot = exitSpot2;
            enterSpot = enterSpot2;
            return true;
        }

        public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            List<IHaulDestination> allHaulDestinationsListInPriorityOrder = map.BaseMapAndVehicleMaps().SelectMany(m => m.haulDestinationManager.AllHaulDestinationsListInPriorityOrder).OrderByDescending(d => d.GetStoreSettings().Priority).ToList();

            Map thingMap = t.SpawnedOrAnyParentSpawned ? t.MapHeld : carrier.MapHeld;
            IntVec3 intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
            IntVec3 intVecOnBase = t.SpawnedOrAnyParentSpawned ? t.PositionHeldOnBaseMap() : carrier.PositionHeldOnBaseMap();
            float num = float.MaxValue;
            StoragePriority storagePriority = StoragePriority.Unstored;
            haulDestination = null;
            for (int i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
            {
                if (!(allHaulDestinationsListInPriorityOrder[i] is ISlotGroupParent) && (!(allHaulDestinationsListInPriorityOrder[i] is Building_Grave) || t.CanBeBuried()))
                {
                    StoragePriority priority = allHaulDestinationsListInPriorityOrder[i].GetStoreSettings().Priority;
                    if (priority < storagePriority || (acceptSamePriority && priority < currentPriority) || (!acceptSamePriority && priority <= currentPriority))
                    {
                        break;
                    }
                    float num2 = (float)intVecOnBase.DistanceToSquared(allHaulDestinationsListInPriorityOrder[i].PositionOnBaseMap());
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
                                IHaulEnroute enroute;
                                if ((enroute = (thing as IHaulEnroute)) != null)
                                {
                                    if (!thing.Map.reservationManager.OnlyReservationsForJobDef(thing, VMF_DefOf.VMF_HaulToContainerAcrossMaps, false))
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
                                    if (!carrier.CanReserveNew(thing, thing.Map))
                                    {
                                        continue;
                                    }
                                }
                                else if (faction != null && thing.Map.reservationManager.IsReservedByAnyoneOf(thing, faction))
                                {
                                    continue;
                                }
                            }
                            if (carrier != null)
                            {
                                TargetInfo exitSpot2;
                                TargetInfo enterSpot2;
                                if (thing != null)
                                {
                                    if (!ReachabilityUtilityOnVehicle.CanReach(thingMap, intVec, thing, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false), thing.Map, out exitSpot2, out enterSpot2))
                                    {
                                        continue;
                                    }
                                }
                                else if (!ReachabilityUtilityOnVehicle.CanReach(thingMap, intVec, allHaulDestinationsListInPriorityOrder[i].Position, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false), allHaulDestinationsListInPriorityOrder[i].Map, out exitSpot2, out enterSpot2))
                                {
                                    continue;
                                }
                                exitSpot = exitSpot2;
                                enterSpot = enterSpot2;
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

        public static bool TryFindBestBetterStoreCellForIn(Thing t, Pawn carrier, StoragePriority currentPriority, Faction faction, ISlotGroup slotGroup, out IntVec3 foundCell, bool needAccurateResult, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            foundCell = IntVec3.Invalid;
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            Map destMap = null;
            Map map = null;
            var owner = slotGroup.Settings.owner;
            if (!(owner is StorageGroup storageGroup))
            {
                if (owner is IHaulDestination haulDestination) map = haulDestination.Map;
                else if (owner is IHaulSource haulSource) map = haulSource.Map;
                else if (owner is ISlotGroupParent slotGroupParent) map = slotGroupParent.Map;
            }
            else
            {
                map = storageGroup.Map;
            }
            float closestDistSquared = 2.14748365E+09f;
            TryFindBestBetterStoreCellForWorker(t, carrier, map, faction, slotGroup, needAccurateResult, ref foundCell, ref closestDistSquared, ref currentPriority, ref exitSpot, ref enterSpot, ref destMap);
            return foundCell.IsValid;
        }

        private static readonly AccessTools.FieldRef<Pawn_PlayerSettings, Dictionary<Map, Area>> allowedAreas = AccessTools.FieldRefAccess<Pawn_PlayerSettings, Dictionary<Map, Area>>("allowedAreas");
    }
}
