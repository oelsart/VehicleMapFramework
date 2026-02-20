using System;
using System.Collections.Generic;
using LudeonTK;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class CrossMapReachabilityCache(World world) : WorldComponent(world)
{
    public static CrossMapReachabilityCache Instance { get; private set; }
    
    private readonly Dictionary<CachedEntry,
        (bool result, TargetInfo exitSpot, TargetInfo enterSpot, List<TraverseSpots> spotsQueue)> cache = [];
    
    private readonly Dictionary<int, HashSet<CachedEntry>> removalDic = [];

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        Instance = Find.World.GetComponent<CrossMapReachabilityCache>();
    }

    public static void ClearCache()
    {
        Instance.cache.Clear();
    }
    
    public static void ClearCacheFor(Map map, bool cleanup = false)
    {
        if (map is null) return;
        ClearInner(map, cleanup);

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(map))
        {
            ClearInner(vehicle.VehicleMap, cleanup);
        }
        return;

        static void ClearInner(Map map, bool cleanup)
        {
            if (Instance.removalDic.TryGetValue(map.uniqueID, out var list))
            {
                foreach (var key in list)
                {
                    if (Instance.cache.TryGetValue(key, out var value))
                    {
                        if (value.spotsQueue is not null)
                        {
                            value.spotsQueue.Clear();
                            SimplePool<List<TraverseSpots>>.Return(value.spotsQueue);
                            value.spotsQueue = null;
                        }
                    }
                    Instance.cache.Remove(key);
                }
                list.Clear();
            }
            if (cleanup) Instance.removalDic.Remove(map.uniqueID);
        }
    }

    public static bool TryGetCache(Region A, Region B, TraverseParmsExtended traverseParms, out bool result,
        out TargetInfo exitSpot, out TargetInfo enterSpot, out List<TraverseSpots> spotsQueue)
    {
        if (A is null || B is null)
        {
            result = false;
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            spotsQueue = null;
            return false;
        }
        if (Instance.cache.TryGetValue(new CachedEntry(A, B, traverseParms), out var value))
        {
            result = value.result;
            exitSpot = value.exitSpot;
            enterSpot = value.enterSpot;
            spotsQueue = value.spotsQueue;
            return true;
        }
        result = false;
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        spotsQueue = null;
        return false;
    }

    public static void Cache(Region A, Region B, TraverseParmsExtended traverseParms, bool result,
        TargetInfo exitSpot, TargetInfo enterSpot, List<TraverseSpots> spotsQueue)
    {
        if (A is null || B is null) return;
        var key = new CachedEntry(A, B, traverseParms);
        Instance.cache[key] = (result, exitSpot, enterSpot, spotsQueue);
        
        if (spotsQueue is null)
        {
            var uniqueIDA = A.Map.uniqueID;
            if (!Instance.removalDic.TryGetValue(uniqueIDA, out var list))
            {
                Instance.removalDic[uniqueIDA] = list = [];
            }
            list.Add(key);

            var uniqueIDB = B.Map.uniqueID;
            if (!Instance.removalDic.TryGetValue(uniqueIDB, out var list2))
            {
                Instance.removalDic[uniqueIDB] = list2 = [];
            }
            list2.Add(key);
            
            return;
        }

        foreach (var spots in spotsQueue)
        {
            if (exitSpot.Map is not null)
            {
                var uniqueID = spots.exitSpot.Map.uniqueID;
                if (!Instance.removalDic.TryGetValue(uniqueID, out var list3))
                {
                    Instance.removalDic[uniqueID] = list3 = [];
                }
                list3.Add(key);
            }

            if (enterSpot.Map is not null)
            {
                var uniqueID2 = spots.enterSpot.Map.uniqueID;
                if (!Instance.removalDic.TryGetValue(uniqueID2, out var list4))
                {
                    Instance.removalDic[uniqueID2] = list4 = [];
                }
                list4.Add(key);
            }
        }
    }

    private readonly struct CachedEntry : IEquatable<CachedEntry>
    {
        private Region FirstRegion { get; }

        private Region SecondRegion { get; }

        private TraverseParmsExtended TraverseParms { get; }

        public CachedEntry(Region firstRegion, Region secondRegion, TraverseParmsExtended traverseParms)
        {
            this = default;
            FirstRegion = firstRegion;
            SecondRegion = secondRegion;
            TraverseParms = traverseParms;
        }

        public static bool operator ==(CachedEntry lhs, CachedEntry rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(CachedEntry lhs, CachedEntry rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override bool Equals(object obj)
        {
            return obj is CachedEntry entry && Equals(entry);
        }

        public bool Equals(CachedEntry other)
        {
            return ReferenceEquals(FirstRegion, other.FirstRegion) &&
                   ReferenceEquals(SecondRegion, other.SecondRegion) &&
                   TraverseParms == other.TraverseParms;
        }

        public override int GetHashCode()
        {
            return Gen.HashCombineStruct(Gen.HashCombineInt(FirstRegion.id, SecondRegion.id), TraverseParms);
        }
    }
    
    [DebugAction(VehicleMapFramework.CategoryName, "Clear CrossMapReachabilityCache")]
    private static void ClearCacheAction()
    {
        ClearCache();
    }   
}
