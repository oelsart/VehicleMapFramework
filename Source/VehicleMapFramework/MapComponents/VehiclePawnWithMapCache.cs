using System;
using System.Collections.Generic;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehiclePawnWithMapCache(Map map) : MapComponent(map)
{
  private readonly List<VehiclePawnWithMap> allVehicles = [];
  public (int lastCachedTick, HashSet<Map> includeItself, HashSet<Map> excludeItself) cachedBaseMapAndVehicleMaps = (-1, [], []);
  public readonly Dictionary<Thing, Vector3> cachedDrawPos = [];
  public readonly Dictionary<VehiclePawn, Rot8> cachedFullRot = [];
  public readonly Dictionary<Thing, IntVec3> cachedPosOnBaseMap = [];
  private int lastCachedFrame = -1;
  private int lastCachedTick = -1;

  public static bool CacheMode { get; set; }

  private static List<VehiclePawnWithMap> EmptyList { get; } = [];

  public override void FinalizeInit()
  {
    VehicleMapParentsComponent.SetCache(map);
    CrossMapMapPawnsCache.RecacheMask();
  }

  public static void RegisterVehicle(VehiclePawnWithMap vehicle)
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      vehicle.Map?.GetComponent<VehiclePawnWithMapCache>()?.allVehicles.AddUnique(vehicle);
      CrossMapMapPawnsCache.RecacheMask();
    });
    
    foreach (var map in Find.Maps)
    {
      if (map.GetComponent<VehiclePawnWithMapCache>() is { } component)
      {
        component.cachedBaseMapAndVehicleMaps.lastCachedTick = -1;
      }
    }
  }

  public static void DeRegisterVehicle(VehiclePawnWithMap vehicle)
  {
    LongEventHandler.ExecuteWhenFinished(CrossMapMapPawnsCache.RecacheMask);
    
    foreach (var map in Find.Maps)
    {
      if (map.GetComponent<VehiclePawnWithMapCache>() is { } component)
      {
        component.allVehicles.Remove(vehicle);
        component.cachedBaseMapAndVehicleMaps.lastCachedTick = -1;
      }
    }

    if (Command_FocusVehicleMap.FocusedVehicle == vehicle)
    {
      Command_FocusVehicleMap.FocusLockedVehicle = null;
      Command_FocusVehicleMap.FocusedVehicle = null;
    }
  }

  public static List<VehiclePawnWithMap> AllVehiclesOn(Map map)
  {
    // ColonyManagerReduxで早期にGetCachedMapComponentが呼ばれてしまった場合、キャッシュにnullが登録されnullを返し続けてしまう
    if (map.mapPawns.AllPawnsSpawnedCount == 0) return EmptyList;

    return map.GetCachedMapComponent<VehiclePawnWithMapCache>()?.allVehicles ?? EmptyList;
  }

  public static ReadOnlySpan<VehiclePawnWithMap> AllVehiclesOnAsReadOnlySpan(Map map)
  {
    if (map.mapPawns.AllPawnsSpawnedCount == 0) return [];

    var component = map.GetCachedMapComponent<VehiclePawnWithMapCache>();
    return component is null ? [] : component.allVehicles.AsReadOnlySpan();
  }

  public void ForceResetPositionCache()
  {
    lastCachedTick = Find.TickManager.TicksGame;
    cachedPosOnBaseMap.Clear();
    cachedFullRot.Clear();
  }

  public void ForceResetDrawPosCache()
  {
    lastCachedFrame = Time.frameCount;
    cachedDrawPos.Clear();
  }

  public void ResetCache()
  {
    if (lastCachedTick != Find.TickManager.TicksGame)
    {
      ForceResetPositionCache();
    }
    if (lastCachedFrame != Time.frameCount)
    {
      ForceResetDrawPosCache(); // PawnのTweenerとの兼ね合いでDrawPosは毎フレームキャッシュクリアせねばならない
    }
  }

  public override void MapComponentUpdate()
  {
    ResetCache();
  }

  public override void MapRemoved()
  {
    VehicleMapParentsComponent.MapRemoved();
    CrossMapReachabilityCache.ClearCacheFor(map, true);
    CrossMapMapPawnsCache.ClearAll();
  }
}
