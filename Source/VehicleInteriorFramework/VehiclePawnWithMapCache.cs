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
            VehiclePawnWithMapCache.allVehicles[map] = new List<VehiclePawnWithMap>();
            VehiclePawnWithMapCache.cachedParentVehicle[this.map] = this.map.Parent is MapParent_Vehicle parentVehicle ? parentVehicle.vehicle : null;
        }

        public override void FinalizeInit()
        {
            var comp = new VehicleMapHolderComp
            {
                parent = this.map.Parent
            };
            this.map.Parent.AllComps.Add(comp);
        }

        public static void ClearCaches()
        {
            VehiclePawnWithMapCache.cachedDrawPos.Clear();
            VehiclePawnWithMapCache.cachedPosOnBaseMap.Clear();
            VehiclePawnWithMapCache.allVehicles.Clear();
        }

        public static void RegisterVehicle(VehiclePawnWithMap vehicle)
        {
            if (!VehiclePawnWithMapCache.allVehicles.ContainsKey(vehicle.Map))
            {
                VehiclePawnWithMapCache.allVehicles[vehicle.Map] = new List<VehiclePawnWithMap>();
            }
            VehiclePawnWithMapCache.allVehicles[vehicle.Map].Add(vehicle);
        }

        public static void DeRegisterVehicle(VehiclePawnWithMap vehicle)
        {
            var list = VehiclePawnWithMapCache.allVehicles.FirstOrDefault(v => v.Value.Contains(vehicle)).Value;
            if (list == null)
            {
                Log.Warning("[VehicleMapFramework] Tried to deregister an unregistered vehicle.");
                return;
            }
            list.Remove(vehicle);
            if (Command_FocusVehicleMap.FocusedVehicle == vehicle)
            {
                Command_FocusVehicleMap.FocuseLockedVehicle = null;
                Command_FocusVehicleMap.FocusedVehicle = null;
            }
        }

        public static IReadOnlyList<VehiclePawnWithMap> AllVehiclesOn(Map map)
        {
            return VehiclePawnWithMapCache.allVehicles[map];
        }

        public static void ForceResetCache()
        {
            VehiclePawnWithMapCache.lastCachedTick = Find.TickManager.TicksGame;
            VehiclePawnWithMapCache.cachedDrawPos.Clear();
            VehiclePawnWithMapCache.cachedPosOnBaseMap.Clear();
        }

        public static void ResetCache()
        {
            if (VehiclePawnWithMapCache.lastCachedTick != Find.TickManager.TicksGame)
            {
                VehiclePawnWithMapCache.lastCachedTick = Find.TickManager.TicksGame;
                VehiclePawnWithMapCache.cachedDrawPos.Clear();
                VehiclePawnWithMapCache.cachedPosOnBaseMap.Clear();
            }
        }

        public override void MapComponentTick()
        {
            ResetCache();
        }

        public static Dictionary<Thing, Vector3> cachedDrawPos = new Dictionary<Thing, Vector3>();

        public static Dictionary<Thing, IntVec3> cachedPosOnBaseMap = new Dictionary<Thing, IntVec3>();

        public static Dictionary<Map, VehiclePawnWithMap> cachedParentVehicle = new Dictionary<Map, VehiclePawnWithMap>();

        public static bool cacheMode = false;

        private static int lastCachedTick = -1;

        private static Dictionary<Map, List<VehiclePawnWithMap>> allVehicles = new Dictionary<Map, List<VehiclePawnWithMap>>();
    }
}
