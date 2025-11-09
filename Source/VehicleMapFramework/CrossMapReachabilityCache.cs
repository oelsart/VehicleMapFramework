using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class CrossMapReachabilityCache(World world) : WorldComponent(world)
{
    private readonly Dictionary<CachedEntry, (bool result, TargetInfo exitSpot, TargetInfo enterSpot)> cache = [];

    public static CrossMapReachabilityCache Instance => Find.World.GetComponent<CrossMapReachabilityCache>();

    public static void ClearCache()
    {
        Instance.cache.Clear();
    }

    public static void ClearCacheFor(Map map)
    {
        Instance.cache.RemoveAll(kvp => kvp.Key.FirstRegion.Map == map || kvp.Key.SecondRegion.Map == map);
    }

    public static bool TryGetCache(Region A, Region B, TraverseParms traverseParms, out bool result, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        if (A is null || B is null)
        {
            result = false;
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            return false;
        }
        if (Instance.cache.TryGetValue(new CachedEntry(A, B, traverseParms), out var value))
        {
            result = value.result;
            exitSpot = value.exitSpot;
            enterSpot = value.enterSpot;
            return true;
        }
        result = false;
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        return false;
    }

    public static void Cache(Region A, Region B, TraverseParms traverseParms, bool result, TargetInfo exitSpot, TargetInfo enterSpot)
    {
        if (A is null || B is null) return;
        var key = new CachedEntry(A, B, traverseParms);
        Instance.cache[key] = (result, exitSpot, enterSpot);
    }

    private struct CachedEntry : IEquatable<CachedEntry>
    {
        public Region FirstRegion { get; }

        public Region SecondRegion { get; }

        private TraverseParms TraverseParms { get; }

        public CachedEntry(Region firstRegion, Region secondRegion, TraverseParms traverseParms)
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

        public readonly override bool Equals(object obj)
        {
            return obj is CachedEntry entry && Equals(entry);
        }

        public readonly bool Equals(CachedEntry other)
        {
            return FirstRegion == other.FirstRegion && SecondRegion == other.SecondRegion && TraverseParms == other.TraverseParms;
        }

        public readonly override int GetHashCode()
        {
            return Gen.HashCombineStruct(Gen.HashCombineInt(FirstRegion.id, SecondRegion.id), TraverseParms);
        }
    }
}
