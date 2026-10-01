using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class StoreAcrossMapsUtility
{
  public static Map tmpDestMap;

  public static bool TryFindBestBetterStoreCellFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority,
    Faction faction, ref IntVec3 foundCell, bool needAccurateResult)
  {
    tmpDestMap = null;
    var invalid = IntVec3.Invalid;
    foreach (var map2 in map.BaseMapAndVehicleMaps(false))
    {
      var storagePriority = currentPriority;
      float num = int.MaxValue;
      foreach (var slotGroup in map2.haulDestinationManager.AllGroupsListInPriorityOrder)
      {
        var priority = slotGroup.Settings.Priority;
        if (priority < storagePriority || priority <= currentPriority)
        {
          break;
        }

        TryFindBestBetterStoreCellForWorker(t, carrier, map2, faction, slotGroup, needAccurateResult, ref invalid,
          ref num, ref storagePriority);
      }
    }

    if (!invalid.IsValid)
    {
      return false;
    }

    foundCell = invalid;
    return true;
  }

  public static void TryFindBestBetterStoreCellForWorker(Thing t, Pawn carrier, Map map, Faction faction,
    ISlotGroup slotGroup, bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared,
    ref StoragePriority foundPriority)
  {
    if (slotGroup == null)
    {
      return;
    }

    if (!slotGroup.Settings.AllowedToAccept(t))
    {
      return;
    }

    var a = t.SpawnedOrAnyParentSpawned
      ? t.PositionHeldOnBaseMap.CellOnAnotherMap(map)
      : carrier.PositionHeldOnBaseMap.CellOnAnotherMap(map);
    var cellsList = slotGroup.CellsList;
    var count = cellsList.Count;
    var num = needAccurateResult ? Mathf.FloorToInt(count * Rand.Range(0.005f, 0.018f)) : 0;
    for (var i = 0; i < count; i++)
    {
      var intVec = cellsList[i];
      float num2 = (a - intVec).LengthHorizontalSquared;
      if (num2 > closestDistSquared || !IsGoodStoreCell(intVec, map, t, carrier, faction)) continue;
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

  public static bool IsGoodStoreCell(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction)
  {
    if (carrier != null && c.IsForbidden(carrier, map))
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

    var thingList = c.GetThingList(map);
    if (thingList.Any(t1 => t1 is IConstructible && GenConstruct.BlocksConstruction(t1, t)))
    {
      return false;
    }

    if (carrier == null) return true;

    IntVec3 start;
    Map startMap;
    if (t.SpawnedParentOrMe is { } spawnedParentOrMe)
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
      startMap = carrier.DepartMapOrPawnMap;
      start = carrier.PositionHeld;
    }

    return CrossMapReachabilityUtility.CanReach(startMap, start, c, PathEndMode.ClosestTouch,
      TraverseParms.For(carrier), map);
  }

  public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map,
    StoragePriority currentPriority, Faction faction, ref IHaulDestination haulDestination, bool acceptSamePriority,
    bool requiresDestReservation)
  {
    var thingMap = t.SpawnedOrAnyParentSpawned ? t.MapHeld : carrier.MapHeld;
    var intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
    var intVecOnBase = t.SpawnedOrAnyParentSpawned ? t.PositionHeldOnBaseMap : carrier.PositionHeldOnBaseMap;
    var num = float.MaxValue;
    var storagePriority = StoragePriority.Unstored;

    foreach (var destMap in map.BaseMapAndVehicleMaps(false))
    {
      foreach (var destination in destMap.haulDestinationManager.AllHaulDestinationsListInPriorityOrder)
      {
        if (destination is ISlotGroupParent || (destination is Building_Grave && !t.CanBeBuried())) continue;

        var priority = destination.GetStoreSettings().Priority;
        if (priority < storagePriority || (acceptSamePriority && priority < currentPriority) ||
            (!acceptSamePriority && priority <= currentPriority))
        {
          break;
        }

        float num2 = intVecOnBase.DistanceToSquared(destination.PositionOnBaseMap());
        if (!(num2 <= num) || !destination.Accepts(t)) continue;
        var thing = destination as Thing;
        if (thing is not null && thing.Faction != faction) continue;
        if (thing is not null)
        {
          if (thing.Faction != faction)
            continue;
          
          if (carrier is not null)
          {
            if (thing.IsForbidden(carrier))
            {
              continue;
            }
          }
          else if (faction is not null && thing.IsForbidden(faction))
          {
            continue;
          }
        }

        if (thing is not null && requiresDestReservation)
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
          else if (carrier is not null)
          {
            if (!carrier.CanReserveNew(thing, thingMap))
            {
              continue;
            }
          }
          else if (faction is not null && thingMap.reservationManager.IsReservedByAnyoneOf(thing, faction))
          {
            continue;
          }
        }

        if (carrier is not null)
        {
          if (thing is not null)
          {
            if (!CrossMapReachabilityUtility.CanReach(thingMap, intVec, thing, PathEndMode.ClosestTouch,
                  TraverseParms.For(carrier), thing.Map))
            {
              continue;
            }
          }
          else if (!CrossMapReachabilityUtility.CanReach(thingMap, intVec, destination.Position, PathEndMode.ClosestTouch,
                     TraverseParms.For(carrier), destination.Map))
          {
            continue;
          }
        }

        num = num2;
        storagePriority = priority;
        haulDestination = destination;
      }
    }

    return haulDestination is not null;
  }

  public static bool NoStorageBlockersIn(IntVec3 c, Map map, Thing thing)
  {
    var list = map.thingGrid.ThingsListAt(c);
    var flag = false;
    for (var i = 0; i < list.Count; i++)
    {
      var thing2 = list[i];
      if (!flag && thing2.def.EverStorable(false) && thing2.CanStackWith(thing) &&
          thing2.stackCount < thing2.def.stackLimit)
      {
        flag = true;
      }

      if (thing2.def.entityDefToBuild != null && thing2.def.entityDefToBuild.passability != Traversability.Standable)
      {
        return false;
      }

      if (thing2.def.surfaceType == SurfaceType.None && thing2.def.passability != Traversability.Standable &&
          (c.GetMaxItemsAllowedInCell(map) <= 1 || thing2.def.category != ThingCategory.Item))
      {
        return false;
      }
    }

    return flag || c.GetItemCount(map) < c.GetMaxItemsAllowedInCell(map);
  }
}