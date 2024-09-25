using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public static class VehicleMapUtility
    {
        public static VehiclePawnWithInterior FocusedVehicle { get; set; }

        public static IEnumerable<Map> ExceptVehicleMaps(this IEnumerable<Map> maps)
        {
            return maps.Where(m => !(m.Parent is MapParent_Vehicle));
        }

        public static Vector3 VehicleMapToOrig(this Vector3 original)
        {
            if (VehicleMapUtility.FocusedVehicle != null)
            {
                var vehicle = VehicleMapUtility.FocusedVehicle;
                return VehicleMapUtility.VehicleMapToOrig(original, vehicle);
            }
            return original;
        }

        public static Vector3 VehicleMapToOrig(this Vector3 original, VehiclePawnWithInterior vehicle)
        {
            var vehiclePos = vehicle.DrawPos.WithY(0f);
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - vehiclePos).RotatedBy(-vehicle.FullRotation.AsAngle) + pivot;
            return drawPos;
        }

        public static Vector3 OrigToVehicleMap(this Vector3 original)
        {
            if (VehicleMapUtility.FocusedVehicle != null)
            {
                var vehicle = VehicleMapUtility.FocusedVehicle;
                return VehicleMapUtility.OrigToVehicleMap(original, vehicle);
            }
            return original;
        }
        public static Vector3 OrigToVehicleMap(this Vector3 original, VehiclePawnWithInterior vehicle)
        {
            var vehiclePos = vehicle.DrawPos.WithY(0f);
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - pivot).RotatedBy(vehicle.FullRotation.AsAngle) + vehiclePos;
            return drawPos;
        }

    }
}
