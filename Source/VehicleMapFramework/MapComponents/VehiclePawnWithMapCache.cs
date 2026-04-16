using System;
using System.Collections.Generic;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehiclePawnWithMapCache(Map map) : MapComponent(map)
{
    public readonly Dictionary<Thing, Vector3> cachedDrawPos = [];

    public readonly Dictionary<Thing, IntVec3> cachedPosOnBaseMap = [];

    public readonly Dictionary<VehiclePawn, Rot8> cachedFullRot = [];

    public readonly (int lastCachedTick, HashSet<Map> hashSet) cachedBaseMapAndVehicleMaps = (-1, []);

    public static bool CacheMode { get; set; }

    private int lastCachedTick = -1;

    private readonly List<VehiclePawnWithMap> allVehicles = [];

    public override void FinalizeInit()
    {
        VehicleMapParentsComponent.SetCachedVehicle(map, map.Parent as MapParent_Vehicle);
        if (MultiFloors.Active && VehicleMapParentsComponent.GetCachedVehicle(map) is null)
        {
            VehicleMapParentsComponent.SetCachedVehicle(map, MultiFloors.GroundMap(map)?.Parent as MapParent_Vehicle);
        }
    }

    public static void RegisterVehicle(VehiclePawnWithMap vehicle)
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            vehicle.Map?.GetComponent<VehiclePawnWithMapCache>()?.allVehicles.AddUnique(vehicle);
        });
    }

    public static void DeRegisterVehicle(VehiclePawnWithMap vehicle)
    {
        foreach (var map in Find.Maps)
        {
            if (map.GetComponent<VehiclePawnWithMapCache>() is { } component)
            {
                component.allVehicles.Remove(vehicle);
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
        return map.GetCachedMapComponent<VehiclePawnWithMapCache>()?.allVehicles ?? [];
    }

    public static ReadOnlySpan<VehiclePawnWithMap> AllVehiclesOnAsReadOnlySpan(Map map)
    {
        var component = map.GetCachedMapComponent<VehiclePawnWithMapCache>();
        return component is null ? [] : component.allVehicles.AsReadOnlySpan();
    }

    public void ForceResetCache()
    {
        lastCachedTick = Find.TickManager.TicksGame;
        cachedPosOnBaseMap.Clear();
        cachedFullRot.Clear();
    }

    public void ResetCache()
    {
        if (lastCachedTick != Find.TickManager.TicksGame || Find.TickManager.Paused)
        {
            ForceResetCache();
        }
        cachedDrawPos.Clear(); // PawnのTweenerとの兼ね合いでDrawPosは毎フレームキャッシュクリアせねばならない
    }

    public override void MapComponentUpdate()
    {
        ResetCache();
    }

    public override void MapRemoved()
    {
        VehicleMapParentsComponent.SetCachedVehicle(map, null);
        CrossMapReachabilityCache.ClearCacheFor(map, true);
    }
}