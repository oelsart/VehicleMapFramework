using HarmonyLib;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Verse;
using static SmashTools.ConditionalPatch;

namespace VehicleInteriors
{
    public static class VehicleMapUtility
    {
        public static VehiclePawnWithInterior FocusedVehicle { get; set; }

        public static IEnumerable<Map> ExceptVehicleMaps(this IEnumerable<Map> maps)
        {
            return maps.Where(m => !(m.Parent is MapParent_Vehicle));
        }

        public static bool IsVehicleMapOf(this Map map, out VehiclePawnWithInterior vehicle)
        {
            if (map?.Parent is MapParent_Vehicle parentVehicle)
            {
                vehicle = parentVehicle.vehicle;
                return true;
            }
            vehicle = null;
            return false;
        }

        public static bool IsOnVehicleMapOf(this Thing thing, out VehiclePawnWithInterior vehicle)
        {
            return thing.Map.IsVehicleMapOf(out vehicle);
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
            var vehicleMapPos = vehicle.cachedDrawPos + VehicleMapUtility.OffsetOf(vehicle);
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - vehicleMapPos).RotatedBy(-vehicle.FullRotation.AsAngle) + pivot;
            return drawPos.WithYOffset(-VehicleMapUtility.altitudeOffset);
        }

        public static IntVec3 VehicleMapToOrig(this IntVec3 original, VehiclePawnWithInterior vehicle)
        {
            return original.ToVector3Shifted().VehicleMapToOrig(vehicle).ToIntVec3();
        }

        public static CellRect VehicleMapToOrig(this CellRect original, VehiclePawnWithInterior vehicle)
        {
            var mapSize = vehicle.interiorMap.Size;
            var vector = new IntVec2(-mapSize.x / 2, -mapSize.z / 2);
            return original.MovedBy(-vehicle.Position).MovedBy(vector);
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
            var vehiclePos = vehicle.cachedDrawPos;
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - pivot).RotatedBy(vehicle.FullRotation.AsAngle) + vehiclePos;
            drawPos += VehicleMapUtility.OffsetOf(vehicle);
            return drawPos.WithYOffset(VehicleMapUtility.altitudeOffset);
        }

        public static IntVec3 OrigToVehicleMap(this IntVec3 original, VehiclePawnWithInterior vehicle)
        {
            return original.ToVector3Shifted().OrigToVehicleMap(vehicle).ToIntVec3();
        }

        public static Vector3 OffsetOf(VehiclePawnWithInterior vehicle)
        {
            var offset = Vector3.zero;
            VehicleMap vehicleMap;
            if ((vehicleMap = vehicle.def.GetModExtension<VehicleMap>()) != null)
            {
                switch (vehicle.FullRotation.AsByte)
                {
                    case Rot8.NorthInt:
                        offset = vehicleMap.offsetNorth.ToVector3();
                        break;

                    case Rot8.EastInt:
                        offset = vehicleMap.offsetEast.ToVector3();
                        break;

                    case Rot8.SouthInt:
                        offset = vehicleMap.offsetSouth.ToVector3();
                        break;

                    case Rot8.WestInt:
                        offset = vehicleMap.offsetWest.ToVector3();
                        break;

                    case Rot8.NorthEastInt:
                        offset = vehicleMap.offsetNorthEast.ToVector3();
                        break;

                    case Rot8.SouthEastInt:
                        offset = vehicleMap.offsetSouthEast.ToVector3();
                        break;

                    case Rot8.SouthWestInt:
                        offset = vehicleMap.offsetSouthWest.ToVector3();
                        break;

                    case Rot8.NorthWestInt:
                        offset = vehicleMap.offsetNorthWest.ToVector3();
                        break;

                    default: break;
                }
            }
            return offset;
        }

        public static List<Type> SelectSectionLayers (List<Type> subClasses, Map map)
        {
            if (map.Parent is MapParent_Vehicle)
            {
                return subClasses.Except(typeof(SectionLayer_ThingsGeneral)).ToList();
            }
            return subClasses.Except(typeof(SectionLayer_ThingsOnVehicle)).ToList();
        }

        public static float PrintExtraRotation(Thing thing)
        {
            float result = 0f;
            if (thing.Map?.Parent is MapParent_Vehicle)
            {
                result -= VehicleMapUtility.rotForPrint.AsAngle;
            }
            return result;
        }

        public static Map BaseMapOfThing(this Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle))
            {
                return vehicle.Map;
            }
            return thing.Map;
        }

        public static IntVec3 PositionOnBaseMap(this Thing thing)
        {
            if (OnVehiclePositionCache.cachedPosOnBaseMap.TryGetValue(thing, out var pos)) return pos;
            return thing.Position;
        }

        public static IntVec3 PositionHeldOnBaseMap(this Thing thing)
        {
            if (thing.Spawned)
            {
                return thing.PositionOnBaseMap();
            }
            IntVec3 rootPosition = IntVec3.Invalid;
            var holder = thing.ParentHolder;
            while (holder != null)
            {
                if (holder is Thing thing2 && thing2.PositionOnBaseMap().IsValid)
                {
                    rootPosition = thing.PositionOnBaseMap();
                }
                else
                {
                    if (holder is ThingComp thingComp && thingComp.parent.PositionOnBaseMap().IsValid)
                    {
                        rootPosition = thingComp.parent.PositionOnBaseMap();
                    }
                }
                holder = holder.ParentHolder;
            }
            if (rootPosition.IsValid)
            {
                return rootPosition;
            }
            return thing.PositionOnBaseMap();
        }

        public static IntVec3 ThingMapToOrig(this IntVec3 origin, Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle))
            {
                return origin.VehicleMapToOrig(vehicle);
            }
            return origin;
        }

        public static IntVec3 OrigToThingMap(this IntVec3 origin, Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle))
            {
                return origin.OrigToVehicleMap(vehicle);
            }
            return origin;
        }

        public static IntVec3 CellOnBaseMap(this ref LocalTargetInfo target)
        {
            if (target.HasThing && OnVehiclePositionCache.cachedPosOnBaseMap.TryGetValue(target.Thing, out var pos)) return pos;
            return target.Cell;
        }

        public static CellRect MovedOccupiedRect(this Thing thing)
        {
            var drawSize = thing.DrawSize;
            return GenAdj.OccupiedRect(thing.PositionOnBaseMap(), thing.Rotation, new IntVec2(Mathf.CeilToInt(drawSize.x), Mathf.CeilToInt(drawSize.y)));
        }

        public static TargetInfo ToBaseMapTargetInfo(ref LocalTargetInfo target, Map map)
        {
            if (!target.IsValid)
            {
                return TargetInfo.Invalid;
            }
            if (target.Thing != null)
            {
                return new TargetInfo(target.Thing);
            }
            return new TargetInfo(target.CellOnBaseMap(), map, false);  
        }

        public static IntVec3 PositionOnAnotherThingMap(this Thing thing, Thing another)
        {
            if (another.IsOnVehicleMapOf(out var vehicle))
            {
                return thing.PositionOnBaseMap().VehicleMapToOrig(vehicle);
            }
            return thing.PositionOnBaseMap();
        }

        public static IntVec3 CellOnAnotherThingMap(this LocalTargetInfo target, Thing another)
        {
            if (target.HasThing)
            {
                return target.Thing.PositionOnAnotherThingMap(another);
            }
            if (another.IsOnVehicleMapOf(out var vehicle))
            {
                return target.Cell.VehicleMapToOrig(vehicle);
            }
            return target.Cell;
        }

        public static Rot4 BaseRotationOfThing(this Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle))
            {
                return new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt);
            }
            return thing.Rotation;
        }

        public static Rot8 BaseFullRotationOfThing(this Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle))
            {
                var rot = new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt);
                return new Rot8(rot, rot.IsHorizontal ? vehicle.Angle : 0f);
            }
            return thing.Rotation;
        }

        public static bool TryGetOnVehicleDrawPos(this Thing thing, ref Vector3 result)
        {
            if (OnVehiclePositionCache.cachedDrawPos.TryGetValue(thing, out var pos))
            {
                result = pos;
                return true;
            }
            else if (!OnVehiclePositionCache.cacheMode && thing.IsOnVehicleMapOf(out var vehicle))
            {
                OnVehiclePositionCache.cacheMode = true;
                OnVehiclePositionCache.cachedDrawPos[thing] = thing.DrawPos.OrigToVehicleMap(vehicle);
                OnVehiclePositionCache.cacheMode = false;
                result = OnVehiclePositionCache.cachedDrawPos[thing];
                return true;
            }
            return false;
        }

        public static Rot4 rotForPrint = Rot4.North;

        public const float altitudeOffset = 0.09615385f;

        public const float altitudeOffsetFull = 7.01923085f;
    }
}
