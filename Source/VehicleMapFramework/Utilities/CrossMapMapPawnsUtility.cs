using System.Collections.Generic;
using RimWorld;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

public class CrossMapMapPawnsCache
{
  private readonly List<Map> tmpMaps = new(128);
  private readonly Dictionary<(Map map, Faction faction), Cache> cacheDict = [];
  private readonly PawnsGetter GetPawns;
  private static List<CrossMapMapPawnsCache> AllInstance { get; } = [];

  public delegate List<Pawn> PawnsGetter(MapPawns instance, Faction faction = null);

  public CrossMapMapPawnsCache(PawnsGetter getter)
  {
    GetPawns = getter;
    AllInstance.Add(this);
  }

  static CrossMapMapPawnsCache()
  {
    GameEvent.OnWorldRemoved += () =>
    {
      foreach (var instance in AllInstance)
      {
        instance.cacheDict.Clear();
      }
    };
  }

  public List<Pawn> Get(Map map, IEnumerable<Pawn> result, Faction faction = null)
  {
    if (!cacheDict.TryGetValue((map, faction), out var cache))
    {
      cache = new Cache();
      cacheDict.Add((map, faction), cache);
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
      list.AddRange(GetPawns(map2.mapPawns, faction));
    }
  }

  public static void RemoveMap(Map map)
  {
    foreach (var instance in AllInstance)
    {
      instance.cacheDict.RemoveAll(x => x.Key.map == map);
    }
  }

  public static void ClearAll()
  {
    foreach (var instance in AllInstance)
    {
      foreach (var cache in instance.cacheDict)
        cache.Value.Clear();
    }
  }

  private class Cache
  {
    public int lastCachedTick = -1;
    public readonly List<Pawn> cachedPawns = [];

    public void Clear()
    {
      lastCachedTick = -1;
      cachedPawns.Clear();
    }
  }
}