using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class CrossMapReachabilityCache : WorldComponent
    {
        public CrossMapReachabilityCache(World world) : base(world) { }

        public static CrossMapReachabilityCache Instance => Find.World.GetComponent<CrossMapReachabilityCache>();

        public override void WorldComponentUpdate()
        {
            if (lastCachedTick != Find.TickManager.TicksGame || Find.TickManager.Paused)
            {
                lastCachedTick = Find.TickManager.TicksGame;
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

        private Dictionary<CachedEntry, (bool result, TargetInfo exitSpot, TargetInfo enterSpot)> cache = new Dictionary<CachedEntry, (bool, TargetInfo, TargetInfo)>();

        private struct CachedEntry : IEquatable<CachedEntry>
        {
            public TargetInfo Depart { get; private set; }

            public TargetInfo Dest { get; private set; }

            public TraverseParms TraverseParms { get; private set; }

            public CachedEntry(TargetInfo depart, TargetInfo dest, TraverseParms traverseParms)
            {
                this = default;
                this.Depart = depart;
                this.Dest = dest;
                this.TraverseParms = traverseParms;
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
                return obj is CachedEntry entry && this.Equals(entry);
            }

            public bool Equals(CachedEntry other)
            {
                return this.Depart == other.Depart && this.Dest == other.Dest && this.TraverseParms == other.TraverseParms;
            }
            public override int GetHashCode()
            {
                return Gen.HashCombineStruct(Gen.HashCombineInt(this.Depart.GetHashCode(), this.Dest.GetHashCode()), this.TraverseParms);
            }
        }
    }
}
