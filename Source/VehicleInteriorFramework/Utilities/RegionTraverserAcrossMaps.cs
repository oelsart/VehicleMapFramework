using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors;

public static class RegionTraverserAcrossMaps
{
    private class BFSWorker
    {
        private Queue<Region> open = new();

        private HashSet<Region> close = [];

        private int numRegionsProcessed;

        private const int skippableRegionSize = 4;

        public void Clear()
        {
            open.Clear();
            close.Clear();
        }

        private void QueueNewOpenRegion(Region region)
        {
            open.Enqueue(region);
            close.Add(region);
        }

        private void FinalizeSearch()
        {
        }

        public void BreadthFirstTraverseWork(Region root, RegionEntryPredicate entryCondition, RegionProcessor regionProcessor, int maxRegions, RegionType traversableRegionTypes)
        {
            if ((root.type & traversableRegionTypes) == 0)
            {
                return;
            }

            Clear();
            numRegionsProcessed = 0;
            open.Enqueue(root);
            while (open.Count > 0)
            {
                Region region = open.Dequeue();
                if (DebugViewSettings.drawRegionTraversal)
                {
                    region.Debug_Notify_Traversed();
                }

                if (regionProcessor != null && regionProcessor(region))
                {
                    FinalizeSearch();
                    return;
                }

                if (ShouldCountRegion(region))
                {
                    numRegionsProcessed++;
                }

                if (numRegionsProcessed >= maxRegions)
                {
                    FinalizeSearch();
                    return;
                }

                foreach (var vehicle2 in region.ListerThings.AllThings.OfType<VehiclePawnWithMap>())
                {
                    var region2 = vehicle2.VehicleMap.regionGrid.AllRegions.FirstOrDefault();
                    if (region2 != null && !open.Contains(region2) && !close.Contains(region2))
                    {
                        QueueNewOpenRegion(region2);
                    }
                }

                for (int i = 0; i < region.links.Count; i++)
                {
                    RegionLink regionLink = region.links[i];
                    for (int j = 0; j < 2; j++)
                    {
                        Region region2 = regionLink.regions[j];
                        if (region2 != null && !open.Contains(region2) && !close.Contains(region2) && (region2.type & traversableRegionTypes) != 0 && (entryCondition == null || entryCondition(region, region2)))
                        {
                            QueueNewOpenRegion(region2);
                        }
                    }
                }

                if (region.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
                {
                    var baseRegion = vehicle.Position.GetRegion(vehicle.Map);
                    if (baseRegion != null && !open.Contains(baseRegion) && !close.Contains(baseRegion))
                    {
                        QueueNewOpenRegion(baseRegion);
                    }
                }
            }

            FinalizeSearch();
        }
    }

    private static Queue<BFSWorker> freeWorkers;

    public static int NumWorkers;

    public static readonly RegionEntryPredicate PassAll;

    public static District FloodAndSetDistricts(Region root, Map map, District existingRoom)
    {
        District floodingDistrict;
        if (existingRoom == null)
        {
            floodingDistrict = District.MakeNew(map);
        }
        else
        {
            floodingDistrict = existingRoom;
        }

        root.District = floodingDistrict;
        if (!root.type.AllowsMultipleRegionsPerDistrict())
        {
            return floodingDistrict;
        }

        bool entryCondition(Region from, Region r) => r.type == root.type && r.District != floodingDistrict;
        bool regionProcessor(Region r)
        {
            r.District = floodingDistrict;
            return false;
        }
        BreadthFirstTraverse(root, entryCondition, regionProcessor, 999999, RegionType.Set_All);
        return floodingDistrict;
    }

    public static void FloodAndSetNewRegionIndex(Region root, int newRegionGroupIndex)
    {
        root.newRegionGroupIndex = newRegionGroupIndex;
        if (root.type.AllowsMultipleRegionsPerDistrict())
        {
            bool entryCondition(Region from, Region r) => r.type == root.type && r.newRegionGroupIndex < 0;
            bool regionProcessor(Region r)
            {
                r.newRegionGroupIndex = newRegionGroupIndex;
                return false;
            }
            BreadthFirstTraverse(root, entryCondition, regionProcessor, 999999, RegionType.Set_All);
        }
    }

    public static bool WithinRegions(this IntVec3 A, IntVec3 B, Map map, int regionLookCount, TraverseParms traverseParams, RegionType traversableRegionTypes = RegionType.Set_Passable)
    {
        Region region = A.GetRegion(map, traversableRegionTypes);
        if (region == null)
        {
            return false;
        }

        Region regB = B.GetRegion(map, traversableRegionTypes);
        if (regB == null)
        {
            return false;
        }

        if (region == regB)
        {
            return true;
        }

        bool entryCondition(Region from, Region r) => r.Allows(traverseParams, isDestination: false);
        bool found = false;
        bool regionProcessor(Region r)
        {
            if (r == regB)
            {
                found = true;
                return true;
            }

            return false;
        }
        BreadthFirstTraverse(region, entryCondition, regionProcessor, regionLookCount, traversableRegionTypes);
        return found;
    }

    public static void MarkRegionsBFS(Region root, RegionEntryPredicate entryCondition, int maxRegions, int inRadiusMark, RegionType traversableRegionTypes = RegionType.Set_Passable)
    {
        BreadthFirstTraverse(root, entryCondition, delegate (Region r)
        {
            r.mark = inRadiusMark;
            return false;
        }, maxRegions, traversableRegionTypes);
    }

    public static bool ShouldCountRegion(Region r)
    {
        return !r.IsDoorway;
    }

    static RegionTraverserAcrossMaps()
    {
        freeWorkers = new Queue<BFSWorker>();
        NumWorkers = 8;
        PassAll = (from, to) => true;
        RecreateWorkers();
    }

    public static void RecreateWorkers()
    {
        freeWorkers.Clear();
        for (int i = 0; i < NumWorkers; i++)
        {
            freeWorkers.Enqueue(new BFSWorker());
        }
    }

    public static void BreadthFirstTraverse(IntVec3 start, Map map, RegionEntryPredicate entryCondition, RegionProcessor regionProcessor, int maxRegions = 999999, RegionType traversableRegionTypes = RegionType.Set_Passable)
    {
        Region region = start.GetRegion(map, traversableRegionTypes);
        if (region != null)
        {
            BreadthFirstTraverse(region, entryCondition, regionProcessor, maxRegions, traversableRegionTypes);
        }
    }

    public static void BreadthFirstTraverse(Region root, RegionProcessorDelegateCache processor, int maxRegions = 999999, RegionType traversableRegionTypes = RegionType.Set_Passable)
    {
        BreadthFirstTraverse(root, processor.RegionEntryPredicateDelegate, processor.RegionProcessorDelegate, maxRegions, traversableRegionTypes);
    }

    public static void BreadthFirstTraverse(Region root, RegionEntryPredicate entryCondition, RegionProcessor regionProcessor, int maxRegions = 999999, RegionType traversableRegionTypes = RegionType.Set_Passable)
    {
        if (freeWorkers.Count == 0)
        {
            Log.Error("No free workers for breadth-first traversal. Either BFS recurred deeper than " + NumWorkers + ", or a bug has put this system in an inconsistent state. Resetting.");
            return;
        }

        if (root == null)
        {
            Log.Error("BreadthFirstTraverse with null root region.");
            return;
        }

        BFSWorker bFSWorker = freeWorkers.Dequeue();
        try
        {
            bFSWorker.BreadthFirstTraverseWork(root, entryCondition, regionProcessor, maxRegions, traversableRegionTypes);
        }
        catch (Exception ex)
        {
            Log.Error("Exception in BreadthFirstTraverse: " + ex.ToString());
        }
        finally
        {
            bFSWorker.Clear();
            freeWorkers.Enqueue(bFSWorker);
        }
    }
}
