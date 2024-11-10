using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehiclePawnWithMapCache : MapComponent
    {
        public VehiclePawnWithMapCache(Map map) : base(map)
        {
            VehiclePawnWithMapCache.allVehicles[this.map] = new List<VehiclePawnWithMap>();
        }

        public static void ClearCaches()
        {
            VehiclePawnWithMapCache.cachedDrawPos.Clear();
            VehiclePawnWithMapCache.cachedPosOnBaseMap.Clear();
            VehiclePawnWithMapCache.allVehicles.Clear();
        }

        public static Dictionary<Thing, Vector3> cachedDrawPos = new Dictionary<Thing, Vector3>();

        public static Dictionary<Thing, IntVec3> cachedPosOnBaseMap = new Dictionary<Thing, IntVec3>();

        public static Dictionary<Map, List<VehiclePawnWithMap>> allVehicles = new Dictionary<Map, List<VehiclePawnWithMap>>();

        public static bool cacheMode = false;
    }
}
