using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class CrossMapReachabilityCache(World world) : WorldComponent(world)
{
    public static CrossMapReachabilityCache Instance => Find.World.GetComponent<CrossMapReachabilityCache>();

    public override void WorldComponentUpdate()
    {
        if (Find.TickManager.TicksAbs - lastCachedTick > 20 || Find.TickManager.Paused)
        {
            lastCachedTick = Find.TickManager.TicksAbs;
            cache.Clear();
        }
    }

    public static bool TryGetCache(TargetInfo depart, TargetInfo dest, TraverseParms traverseParms, out bool result, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        if (Instance.cache.TryGetValue(new CachedEntry(depart, dest, traverseParms), out var value))
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

    public static void Cache(TargetInfo depart, TargetInfo dest, TraverseParms traverseParms, bool result, TargetInfo exitSpot, TargetInfo enterSpot)
    {
        var key = new CachedEntry(depart, dest, traverseParms);
        Instance.cache[key] = (result, exitSpot, enterSpot);
    }

    private int lastCachedTick;

    private Dictionary<CachedEntry, (bool result, TargetInfo exitSpot, TargetInfo enterSpot)> cache = [];

    private struct CachedEntry : IEquatable<CachedEntry>
    {
        public TargetInfo Depart { get; private set; }

        public TargetInfo Dest { get; private set; }

        public TraverseParms TraverseParms { get; private set; }

        public CachedEntry(TargetInfo depart, TargetInfo dest, TraverseParms traverseParms)
        {
            this = default;
            Depart = depart;
            Dest = dest;
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
            return Depart == other.Depart && Dest == other.Dest && TraverseParms == other.TraverseParms;
        }
        public override readonly int GetHashCode()
        {
            return Gen.HashCombineStruct(Gen.HashCombineInt(Depart.GetHashCode(), Dest.GetHashCode()), TraverseParms);
        }
    }
}
