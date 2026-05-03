using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CrossMapMapPawnsCache(CrossMapMapPawnsCache.GetPawns GetPawns)
{
    private readonly List<Map> tmpMaps = new(128);
    private readonly ConditionalWeakTable<Map, Cache> cacheDict = [];
    public delegate List<Pawn> GetPawns(MapPawns instance, Faction faction = null);
    
    public List<Pawn> Get(Map map, IEnumerable<Pawn> result, Faction faction = null)
    {
        if (!cacheDict.TryGetValue(map, out var cache))
        {
            cache = new Cache();
            cacheDict.Add(map, cache);
        }
        if (cache.lastCachedTick != GenTicks.TicksGame)
        {
            cache.lastCachedTick = GenTicks.TicksGame;
            Sum(map, result, cache.cachedPawns, faction);
        }
        return cache.cachedPawns;
    }

    private void Sum(Map map, IEnumerable<Pawn> result, List<Pawn> list, Faction faction)
    {
        list.Clear();
        list.AddRange(result);
        tmpMaps.Clear();
        map.VehicleMapsOnMap(tmpMaps);
        foreach (var map2 in tmpMaps.AsReadOnlySpan())
        {
            var allPawns = GetPawns(map2.mapPawns, faction);
            for (var i = 0; i < allPawns.Count; i++)
            {
                list.Add(allPawns[i]);
            }
        }
    }

    private class Cache
    {
        public int lastCachedTick = -1;
        public readonly List<Pawn> cachedPawns = [];
    }
}
