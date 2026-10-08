using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

public class CrossMapMapPawnsCache
{
  private readonly List<Map> tmpMaps = [with(128)];
  private readonly PawnsGetter GetPawns;
  private Cache[] caches = new Cache[128];
  private static ulong lowMask; // マップ上に車両が存在しない場合早期returnするためのマスク
  private static ulong highMask;

  internal int CacheCount => caches.NonNull.Count();
  
  internal static List<CrossMapMapPawnsCache> AllInstances { get; } = [];

  public delegate IReadOnlyList<Pawn> PawnsGetter(MapPawns instance, Faction faction = null);

  public CrossMapMapPawnsCache(PawnsGetter getter)
  {
    GetPawns = getter;
    AllInstances.Add(this);
  }

  static CrossMapMapPawnsCache()
  {
    GameEvent.OnWorldRemoved += () =>
    {
      foreach (var instance in AllInstances)
      {
        Array.Clear(instance.caches, 0, instance.caches.Length);
      }
    };
  }

  public IReadOnlyList<Pawn> Get(Map map, IReadOnlyList<Pawn> result, Faction faction = null)
  {
    var mapIndex = VehicleMapParentsComponent.GetMapIndex(map);
    if (mapIndex < 64)
    {
      if ((lowMask & 1UL << mapIndex) == 0UL)
        return result;
    }
    else
    {
      if ((highMask & 1UL << mapIndex - 64) == 0UL)
        return result;
    }
    
    
    var index = ((faction?.loadID ?? 0) << 4) | (mapIndex & 0xFF);
    if (caches.Length <= index)
    {
      Array.Resize(ref caches, Math.Max(caches.Length * 2, index + 1));
    }

    ref var cache = ref caches[index];
    cache ??= new Cache();

    if (cache.dirty)
    {
      cache.dirty = false;
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

  public static void RecacheMask()
  {
    lowMask = 0UL;
    highMask = 0UL;
    foreach (var map in Find.Maps)
    {
      if (VehiclePawnWithMapCache.AllVehiclesOn(map).Count != 0 ||
          map.IsVehicleMapOf(out var vehicle) && vehicle.VehicleCaravanOrStashedVehicle is not null)
      {
        var index = VehicleMapParentsComponent.GetMapIndex(map);
        if (index < 64)
        {
          lowMask |= 1UL << index;
        }
        else
        {
          highMask |= 1UL << index - 64;
        }
      }
    }
  }

  public static void ClearAll()
  {
    foreach (var instance in AllInstances)
    {
      Array.Clear(instance.caches, 0, instance.caches.Length);
    }
  }

  public static void DirtyAll()
  {
    foreach (var instance in AllInstances)
    {
      foreach (var cache in instance.caches)
      {
        cache?.dirty = true;
      }
    }
  }

  private class Cache
  {
    public bool dirty = true;
    public readonly List<Pawn> cachedPawns = [];
  }
}