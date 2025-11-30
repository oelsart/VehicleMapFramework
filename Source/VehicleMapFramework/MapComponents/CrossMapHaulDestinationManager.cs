using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CrossMapHaulDestinationManager(Map map) : MapComponent(map)
{
    public List<IHaulDestination> AllHaulDestinationsListInPriorityOrder { get; } = [];

    public List<SlotGroup> AllGroupsListForReading { get; } = [];

    public List<SlotGroup> AllGroupsListInPriorityOrder => AllGroupsListForReading;

    //60tickごとにベースマップのコンポーネントにHaulDestinationを登録する。vehicleがDespawnした時にRemoveされる
    public override void MapComponentTick()
    {
        base.MapComponentTick();
        if (map.IsHashIntervalTick(60))
        {
            var baseMap = map.BaseMap();
            if (map == baseMap)
            {
                if (!map.IsVehicleMapOf(out var vehicle) ||
                    vehicle.GetVehicleCaravan() is not { } caravan ||
                    !caravan.TryGetComponent<CaravanHaulDestinationManager>(out var comp))
                    return;

                var destinations = comp.AllHaulDestinationsListInPriorityOrder;
                AllHaulDestinationsListInPriorityOrder.Where(h => !destinations.Contains(h)).Do(comp.AddHaulDestination);
            }

            var baseMapComponent = baseMap.GetCachedMapComponent<CrossMapHaulDestinationManager>();

            var baseMapDestinations = baseMapComponent.AllHaulDestinationsListInPriorityOrder;
            AllHaulDestinationsListInPriorityOrder.Where(h => !baseMapDestinations.Contains(h)).Do(baseMapComponent.AddHaulDestination);
        }
    }

    public void AddHaulDestination(IHaulDestination haulDestination)
    {
        if (AllHaulDestinationsListInPriorityOrder.Contains(haulDestination))
        {
            //車両マップから下のマップに転写するので、GravshipVehicleでHaulDestinationが再び下のマップに登録されてしまうこともあるわなと思ってエラーを無効化
            //VMF_Log.Error("Double-added haul destination " + haulDestination.ToStringSafe());
            return;
        }

        AllHaulDestinationsListInPriorityOrder.Add(haulDestination);
        AllHaulDestinationsListInPriorityOrder.InsertionSort(CompareHaulDestinationPrioritiesDescending);
        if (haulDestination is not ISlotGroupParent slotGroupParent)
        {
            return;
        }

        var slotGroup = slotGroupParent.GetSlotGroup();
        if (slotGroup == null)
        {
            VMF_Log.Error("ISlotGroupParent gave null slot group: " + slotGroupParent.ToStringSafe());
            return;
        }

        AllGroupsListForReading.Add(slotGroup);
        AllGroupsListForReading.InsertionSort(CompareSlotGroupPrioritiesDescending);
    }

    public void RemoveHaulDestination(IHaulDestination haulDestination)
    {
        //if (!allHaulDestinationsInOrder.Contains(haulDestination))
        //{
        //    VMF_Log.Error("Removing haul destination that isn't registered " + haulDestination.ToStringSafe());
        //    return;
        //}

        AllHaulDestinationsListInPriorityOrder.Remove(haulDestination);
        if (haulDestination is not ISlotGroupParent slotGroupParent)
        {
            return;
        }

        var slotGroup = slotGroupParent.GetSlotGroup();
        if (slotGroup == null)
        {
            VMF_Log.Error("ISlotGroupParent gave null slot group: " + slotGroupParent.ToStringSafe());
            return;
        }

        AllGroupsListForReading.Remove(slotGroup);
    }

    public void Notify_HaulDestinationChangedPriority()
    {
        AllHaulDestinationsListInPriorityOrder.InsertionSort(CompareHaulDestinationPrioritiesDescending);
        AllGroupsListForReading.InsertionSort(CompareSlotGroupPrioritiesDescending);

        var baseMap = map.BaseMap();
        if (map != baseMap)
        {
            baseMap.GetCachedMapComponent<CrossMapHaulDestinationManager>().Notify_HaulDestinationChangedPriority();
        }
    }

    private static int CompareHaulDestinationPrioritiesDescending(IHaulDestination a, IHaulDestination b)
    {
        return ((int)b.GetStoreSettings().Priority).CompareTo((int)a.GetStoreSettings().Priority);
    }

    private static int CompareHaulSourcePrioritiesDescending(IHaulSource a, IHaulSource b)
    {
        return ((int)b.GetStoreSettings().Priority).CompareTo((int)a.GetStoreSettings().Priority);
    }

    private static int CompareSlotGroupPrioritiesDescending(SlotGroup a, SlotGroup b)
    {
        return ((int)b.Settings.Priority).CompareTo((int)a.Settings.Priority);
    }
}
