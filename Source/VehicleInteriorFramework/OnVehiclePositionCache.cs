using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public static class OnVehiclePositionCache
    {
        public static Dictionary<Thing, Vector3> cachedDrawPos = new Dictionary<Thing, Vector3>();

        public static Dictionary<Thing, IntVec3> cachedPosOnBaseMap = new Dictionary<Thing, IntVec3>();

        public static bool cacheMode = false;
    }
}
