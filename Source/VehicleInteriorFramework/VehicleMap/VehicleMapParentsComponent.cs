using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class VehicleMapParentsComponent : WorldComponent
    {
        public static Dictionary<Map, Lazy<VehiclePawnWithMap>> CachedParentVehicle => cachedParentVehicle;

        public VehicleMapParentsComponent(World world) : base(world)
        {
            Command_FocusVehicleMap.FocuseLockedVehicle = null;
            Command_FocusVehicleMap.FocusedVehicle = null;
        }

        public override void FinalizeInit()
        {
            cachedParentVehicle.Clear();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref this.vehicleMaps, "vehicleMaps", LookMode.Deep);
        }

        public List<MapParent_Vehicle> vehicleMaps = new List<MapParent_Vehicle>();

        private static Dictionary<Map, Lazy<VehiclePawnWithMap>> cachedParentVehicle = new Dictionary<Map, Lazy<VehiclePawnWithMap>>();
    }
}