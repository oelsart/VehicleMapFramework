using HarmonyLib;
using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors
{
    public class CrossMapHaulDestinationManager : MapComponent
    {
        private readonly List<IHaulDestination> allHaulDestinationsInOrder = new List<IHaulDestination>();

        private readonly List<IHaulSource> allHaulSourcesInOrder = new List<IHaulSource>();

        private readonly List<SlotGroup> allGroupsInOrder = new List<SlotGroup>();

        public IEnumerable<IHaulDestination> AllHaulDestinations => allHaulDestinationsInOrder;

        public List<IHaulDestination> AllHaulDestinationsListForReading => allHaulDestinationsInOrder;

        public List<IHaulDestination> AllHaulDestinationsListInPriorityOrder => allHaulDestinationsInOrder;

        public List<IHaulSource> AllHaulSourcesListInPriorityOrder => allHaulSourcesInOrder;

        public IEnumerable<SlotGroup> AllGroups => allGroupsInOrder;

        public List<SlotGroup> AllGroupsListForReading => allGroupsInOrder;

        public List<IHaulSource> AllHaulSourcesListForReading => allHaulSourcesInOrder;

        public List<SlotGroup> AllGroupsListInPriorityOrder => allGroupsInOrder;

        public IEnumerable<TargetInfo> AllSlots
        {
            get
            {
                for (int i = 0; i < allGroupsInOrder.Count; i++)
                {
                    var map = allGroupsInOrder[i].parent.Map;
                    List<IntVec3> cellsList = allGroupsInOrder[i].CellsList;
                    int j = 0;
                    while (j < allGroupsInOrder.Count)
                    {
                        yield return new TargetInfo(cellsList[j], map);
                        i++;
                    }
                }
            }
        }

        public CrossMapHaulDestinationManager(Map map) : base(map) { }

        //60tickごとにベースマップのコンポーネントにHaulDestinationを登録する。vehicleがDespawnした時にRemoveされる
        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (map.IsHashIntervalTick(60))
            {
                var baseMap = map.BaseMap();
                if (map == baseMap) return;

                var baseMapComponent = baseMap.GetCachedMapComponent<CrossMapHaulDestinationManager>();

                var baseMapDestinations = baseMapComponent.allHaulDestinationsInOrder;
                allHaulDestinationsInOrder.Where(h => !baseMapDestinations.Contains(h)).Do(h => baseMapComponent.AddHaulDestination(h));

                var baseMapSources = baseMapComponent.allHaulSourcesInOrder;
                allHaulSourcesInOrder.Where(s => !baseMapSources.Contains(s)).Do(s => baseMapComponent.AddHaulSource(s));
            }
        }

        public void AddHaulDestination(IHaulDestination haulDestination)
        {
            if (allHaulDestinationsInOrder.Contains(haulDestination))
            {
                Log.Error("Double-added haul destination " + haulDestination.ToStringSafe());
                return;
            }

            allHaulDestinationsInOrder.Add(haulDestination);
            allHaulDestinationsInOrder.InsertionSort(CompareHaulDestinationPrioritiesDescending);
            if (!(haulDestination is ISlotGroupParent slotGroupParent))
            {
                return;
            }

            SlotGroup slotGroup = slotGroupParent.GetSlotGroup();
            if (slotGroup == null)
            {
                Log.Error("ISlotGroupParent gave null slot group: " + slotGroupParent.ToStringSafe());
                return;
            }

            allGroupsInOrder.Add(slotGroup);
            allGroupsInOrder.InsertionSort(CompareSlotGroupPrioritiesDescending);
        }

        public void RemoveHaulDestination(IHaulDestination haulDestination)
        {
            if (!allHaulDestinationsInOrder.Contains(haulDestination))
            {
                Log.Error("Removing haul destination that isn't registered " + haulDestination.ToStringSafe());
                return;
            }

            allHaulDestinationsInOrder.Remove(haulDestination);
            if (!(haulDestination is ISlotGroupParent slotGroupParent))
            {
                return;
            }

            SlotGroup slotGroup = slotGroupParent.GetSlotGroup();
            if (slotGroup == null)
            {
                Log.Error("ISlotGroupParent gave null slot group: " + slotGroupParent.ToStringSafe());
                return;
            }

            allGroupsInOrder.Remove(slotGroup);
        }

        public void AddHaulSource(IHaulSource source)
        {
            if (allHaulSourcesInOrder.Contains(source))
            {
                Log.Error("Double-added haul destination " + source.ToStringSafe());
                return;
            }

            allHaulSourcesInOrder.Add(source);
            allHaulSourcesInOrder.InsertionSort(CompareHaulSourcePrioritiesDescending);
        }

        public void RemoveHaulSource(IHaulSource source)
        {
            if (!allHaulSourcesInOrder.Remove(source))
            {
                Log.Error("Removing haul source that isn't registered " + source.ToStringSafe());
            }
        }

        public void Notify_HaulDestinationChangedPriority()
        {
            allHaulDestinationsInOrder.InsertionSort(CompareHaulDestinationPrioritiesDescending);
            allGroupsInOrder.InsertionSort(CompareSlotGroupPrioritiesDescending);
            allHaulSourcesInOrder.InsertionSort(CompareHaulSourcePrioritiesDescending);

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
}
