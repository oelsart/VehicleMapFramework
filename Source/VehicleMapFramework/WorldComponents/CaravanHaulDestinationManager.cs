using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CaravanHaulDestinationManager : WorldObjectComp
{
  private List<VehiclePawn> Vehicles => field ??= parent.Vehicles.ToList();

  public List<IHaulDestination> AllHaulDestinationsListInPriorityOrder { get; } = [];

  public List<SlotGroup> AllGroupsListInPriorityOrder { get; } = [];

  public override void CompTick()
  {
    if (!Vehicles.SequenceEqual(parent.Vehicles))
    {
      Vehicles.Clear();
      Vehicles.AddRange(parent.Vehicles);
      AllHaulDestinationsListInPriorityOrder.Clear();
      AllGroupsListInPriorityOrder.Clear();
    }
  }

  public void AddHaulDestination(IHaulDestination haulDestination)
  {
    if (AllHaulDestinationsListInPriorityOrder.Contains(haulDestination))
    {
      VMF_Log.Error("Double-added haul destination " + haulDestination.ToStringSafe());
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

    AllGroupsListInPriorityOrder.Add(slotGroup);
    AllGroupsListInPriorityOrder.InsertionSort(CompareSlotGroupPrioritiesDescending);
  }

  private static int CompareHaulDestinationPrioritiesDescending(IHaulDestination a, IHaulDestination b)
  {
    return ((int)b.GetStoreSettings().Priority).CompareTo((int)a.GetStoreSettings().Priority);
  }

  private static int CompareSlotGroupPrioritiesDescending(SlotGroup a, SlotGroup b)
  {
    return ((int)b.Settings.Priority).CompareTo((int)a.Settings.Priority);
  }
}
