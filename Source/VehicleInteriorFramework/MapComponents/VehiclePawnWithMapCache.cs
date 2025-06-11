using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehiclePawnWithMapCache : MapComponent
    {
        public VehiclePawnWithMapCache(Map map) : base(map)
        {
            VehicleMapParentsComponent.CachedParentVehicle[this.map] = new Lazy<VehiclePawnWithMap>(() =>
            {
                if (this.map.Parent is MapParent_Vehicle parentVehicle)
                {
                    return parentVehicle.vehicle;
                }
                return null;
            }, false);
        }

        public override void MapRemoved()
        {
            bool WorldObjectSpawned(IThingHolder holder)
            {
                while (holder != null)
                {
                    if (holder is WorldObject worldObject)
                    {
                        return worldObject.Spawned;
                    }
                    holder = holder.ParentHolder;
                }
                return false;
            }

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var mapParent_Vehicle in Find.World.GetComponent<VehicleMapParentsComponent>().vehicleMaps.ToArray())
                {
                    var vehicle = mapParent_Vehicle.vehicle;
                    if (!vehicle.SpawnedOrAnyParentSpawned && !WorldObjectSpawned(vehicle) && !vehicle.Destroyed)
                    {
                        vehicle.Destroy();
                    }
                }
            });
        }

        public static void RegisterVehicle(VehiclePawnWithMap vehicle)
        {
            MapComponentCache<VehiclePawnWithMapCache>.GetComponent(vehicle.Map).allVehicles.Add(vehicle);
        }

        public static void DeRegisterVehicle(VehiclePawnWithMap vehicle)
        {
            var hashSet = Find.Maps.Select(m => MapComponentCache<VehiclePawnWithMapCache>.GetComponent(m).allVehicles).FirstOrDefault(h => h.Contains(vehicle));
            if (hashSet == null)
            {
                Log.Warning("[VehicleMapFramework] Tried to deregister an unregistered vehicle.");
                return;
            }
            hashSet.Remove(vehicle);
            if (Command_FocusVehicleMap.FocusedVehicle == vehicle)
            {
                Command_FocusVehicleMap.FocuseLockedVehicle = null;
                Command_FocusVehicleMap.FocusedVehicle = null;
            }
        }

        public static IReadOnlyCollection<VehiclePawnWithMap> TryGetAllVehiclesOn(Map map)
        {
            //ColonyManagerReduxでコンポーネント構築中に呼ばれてしまうため、nullを想定する必要がある
            return map.GetComponent<VehiclePawnWithMapCache>()?.allVehicles ?? new HashSet<VehiclePawnWithMap>();
        }

        public static IReadOnlyCollection<VehiclePawnWithMap> AllVehiclesOn(Map map)
        {
            return map.GetCachedMapComponent<VehiclePawnWithMapCache>().allVehicles;
        }

        public void ForceResetCache()
        {
            lastCachedTick = Find.TickManager.TicksGame;
            cachedDrawPos.Clear();
            cachedPosOnBaseMap.Clear();
            CacheDrawPos();
        }

        public void ResetCache()
        {
            if (lastCachedTick != Find.TickManager.TicksGame || Find.TickManager.Paused)
            {
                lastCachedTick = Find.TickManager.TicksGame;
                //cachedDrawPos.Keys.Except(map.listerThings.AllThings).ToArray().ForEach(t => cachedDrawPos.Remove(t));
                cachedDrawPos.Clear();
                cachedPosOnBaseMap.Clear();
                //CacheDrawPos();
            }
        }

        private void CacheDrawPos()
        {
            //if (map.IsVehicleMapOf(out var vehicle))
            //{
            //    cacheMode = true;
            //    if (vehicle.vehiclePather?.Moving ?? false)
            //    {
            //        map.listerThings.AllThings.ForEach(t =>
            //        {
            //            cachedDrawPos[t] = t.DrawPos.ToBaseMapCoord(vehicle);
            //        });
            //    }
            //    else
            //    {
            //        map.dynamicDrawManager.DrawThings.ForEach(t =>
            //        {
            //            cachedDrawPos[t] = t.DrawPos.ToBaseMapCoord(vehicle);
            //        });
            //    }
            //    cacheMode = false;
            //}
        }

        public override void MapComponentUpdate()
        {
            if (map.IsVehicleMapOf(out _))
            {
                ResetCache();
            }
        }

        public Dictionary<Thing, Vector3> cachedDrawPos = new Dictionary<Thing, Vector3>();

        public Dictionary<Thing, IntVec3> cachedPosOnBaseMap = new Dictionary<Thing, IntVec3>();

        private int lastCachedTick = -1;

        public bool cacheMode;

        private HashSet<VehiclePawnWithMap> allVehicles = new HashSet<VehiclePawnWithMap>();
    }
}