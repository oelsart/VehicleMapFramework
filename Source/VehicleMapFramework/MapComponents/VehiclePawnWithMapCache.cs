using System.Collections.Generic;
using System.Linq;
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
        MapComponentCache<VehiclePawnWithMapCache>.GetComponent(vehicle.Map).allVehicles.AddUnique(vehicle);
    }

    public static void DeRegisterVehicle(VehiclePawnWithMap vehicle)
    {
        var hashSet = Find.Maps.Select(m => MapComponentCache<VehiclePawnWithMapCache>.GetComponent(m).allVehicles).FirstOrDefault(h => h.Contains(vehicle));
        if (hashSet == null)
        {
            VMF_Log.Warning("Tried to deregister an unregistered vehicle.");
            return;
        }
        hashSet.Remove(vehicle);
        if (Command_FocusVehicleMap.FocusedVehicle != vehicle) return;
        
        Command_FocusVehicleMap.FocusLockedVehicle = null;
        Command_FocusVehicleMap.FocusedVehicle = null;
    }

    public static IReadOnlyCollection<VehiclePawnWithMap> AllVehiclesOn(Map map)
    {
        return map.GetCachedMapComponent<VehiclePawnWithMapCache>()?.allVehicles ?? [];
    }

    public void ForceResetCache()
    {
        lastCachedTick = Find.TickManager.TicksGame;
        cachedDrawPos.Clear();
        cachedPosOnBaseMap.Clear();
        cachedFullRot.Clear();
    }

    public void ResetCache()
    {
        if (lastCachedTick != Find.TickManager.TicksGame || Find.TickManager.Paused)
        {
            ForceResetCache();
        }
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