using RimWorld;
using SmashTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class AsyncReachabilityCache
    {
        private struct CachedEntry : IEquatable<CachedEntry>
        {
            public int FirstID { get; private set; }

            public int SecondID { get; private set; }

            public TraverseParms TraverseParms { get; private set; }

            public CachedEntry(int firstID, int secondID, TraverseParms traverseParms)
            {
                this = default;
                if (firstID < secondID)
                {
                    FirstID = firstID;
                    SecondID = secondID;
                }
                else
                {
                    FirstID = secondID;
                    SecondID = firstID;
                }

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
                if (!(obj is CachedEntry))
                {
                    return false;
                }

                return Equals((CachedEntry)obj);
            }

            public bool Equals(CachedEntry other)
            {
                if (FirstID == other.FirstID && SecondID == other.SecondID)
                {
                    return TraverseParms == other.TraverseParms;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return Gen.HashCombineStruct(Gen.HashCombineInt(FirstID, SecondID), TraverseParms);
            }
        }

        private ConcurrentDictionary<CachedEntry, bool> cacheDict = new ConcurrentDictionary<CachedEntry, bool>();

        private static ConcurrentSet<AsyncReachabilityCache.CachedEntry> tmpCachedEntries = new ConcurrentSet<AsyncReachabilityCache.CachedEntry>();

        public int Count => cacheDict.Count;

        public BoolUnknown CachedResultFor(District A, District B, TraverseParms traverseParams)
        {
            if (cacheDict.TryGetValue(new CachedEntry(A.ID, B.ID, traverseParams), out var value))
            {
                if (!value)
                {
                    return BoolUnknown.False;
                }

                return BoolUnknown.True;
            }

            return BoolUnknown.Unknown;
        }

        public void AddCachedResult(District A, District B, TraverseParms traverseParams, bool reachable)
        {
            CachedEntry key = new CachedEntry(A.ID, B.ID, traverseParams);
            if (!cacheDict.ContainsKey(key))
            {
                cacheDict[key] = reachable;
            }
        }

        public void Clear()
        {
            cacheDict.Clear();
        }

        public void ClearFor(Pawn p)
        {
            tmpCachedEntries.Clear();
            foreach (KeyValuePair<CachedEntry, bool> item in cacheDict)
            {
                if (item.Key.TraverseParms.pawn == p)
                {
                    tmpCachedEntries.Add(item.Key);
                }
            }

            foreach (var entry in tmpCachedEntries.Keys)
            {
                cacheDict.TryRemove(entry, out _);
            }

            tmpCachedEntries.Clear();
        }

        public void ClearForHostile(Thing hostileTo)
        {
            tmpCachedEntries.Clear();
            foreach (KeyValuePair<CachedEntry, bool> item in cacheDict)
            {
                Pawn pawn = item.Key.TraverseParms.pawn;
                if (pawn != null && pawn.HostileTo(hostileTo))
                {
                    tmpCachedEntries.Add(item.Key);
                }
            }

            foreach (var entry in tmpCachedEntries.Keys)
            {
                cacheDict.TryRemove(entry, out _);
            }

            tmpCachedEntries.Clear();
        }
    }
}
