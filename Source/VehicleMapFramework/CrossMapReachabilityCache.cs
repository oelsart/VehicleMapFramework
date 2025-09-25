using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class CrossMapReachabilityCache(World world) : WorldComponent(world)
{
    private Dictionary<CachedEntry, (bool result, TargetInfo exitSpot, TargetInfo enterSpot)> cache = [];

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
        if (A == null || B == null)
        {
            result = false;
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            return true;
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
        var key = new CachedEntry(A, B, traverseParms);
        Instance.cache[key] = (result, exitSpot, enterSpot);
    }

    private struct CachedEntry : IEquatable<CachedEntry>
    {
        public Region FirstRegion { readonly get; private set; }

        public Region SecondRegion { readonly get; private set; }

        public TraverseParms TraverseParms { get; private set; }

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

        public override readonly bool Equals(object obj)
        {
            return obj is CachedEntry entry && Equals(entry);
        }

        public readonly bool Equals(CachedEntry other)
        {
            return FirstRegion == other.FirstRegion && SecondRegion == other.SecondRegion && TraverseParms == other.TraverseParms;
        }

        public override readonly int GetHashCode()
        {
            return Gen.HashCombineStruct(Gen.HashCombineInt(FirstRegion.id, SecondRegion.id), TraverseParms);
        }
    }
}
