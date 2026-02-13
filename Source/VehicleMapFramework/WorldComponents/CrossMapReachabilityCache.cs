using System;
using System.Collections.Generic;
using LudeonTK;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class CrossMapReachabilityCache(World world) : WorldComponent(world)
{
    private readonly Dictionary<CachedEntry,
        (bool result, TargetInfo exitSpot, TargetInfo enterSpot, List<(TargetInfo, TargetInfo)> spotsQueue)> cache = [];
    
    private readonly List<CachedEntry> removalList = [];
    
    public static CrossMapReachabilityCache Instance => Find.World.GetComponent<CrossMapReachabilityCache>();

    public static void ClearCache()
    {
        Instance.cache.Clear();
    }
    
    public static void ClearCacheFor(Map map)
    {
        var instance = Instance;
        instance.removalList.Clear();
    
        foreach (var key in instance.cache.Keys)
        {
            if (key.FirstRegion?.Map == map || key.SecondRegion?.Map == map)
                instance.removalList.Add(key);
        }
        foreach (var key in instance.removalList)
            instance.cache.Remove(key);
    }

    public static bool TryGetCache(Region A, Region B, TraverseParmsExtended traverseParms, out bool result,
        out TargetInfo exitSpot, out TargetInfo enterSpot, out List<(TargetInfo, TargetInfo)> spotsQueue)
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
        TargetInfo exitSpot, TargetInfo enterSpot, List<(TargetInfo, TargetInfo)> spotsQueue)
    {
        if (A is null || B is null) return;
        var key = new CachedEntry(A, B, traverseParms);
        Instance.cache[key] = (result, exitSpot, enterSpot, spotsQueue);
    }

    private readonly struct CachedEntry : IEquatable<CachedEntry>
    {
        public Region FirstRegion { get; }

        public Region SecondRegion { get; }

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
