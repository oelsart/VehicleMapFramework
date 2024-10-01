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
            if (thing.Map.Parent is MapParent_Vehicle)
            {
                result -= VehicleMapUtility.rotForPrint.AsAngle;
            }
            return result;
        }

        public static Map BaseMapOfThing(this Thing thing)
        {
            if (thing.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                return parentVehicle.vehicle.Map;
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
                        rootPosition = thingComp.parent.Position;
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
            if (thing.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                return origin.VehicleMapToOrig(parentVehicle.vehicle);
            }
            return origin;
        }

        public static IntVec3 OrigToThingMap(this IntVec3 origin, Thing thing)
        {
            if (thing.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                return origin.OrigToVehicleMap(parentVehicle.vehicle);
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
            if (another.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                return thing.PositionOnBaseMap().VehicleMapToOrig(parentVehicle.vehicle);
            }
            return thing.PositionOnBaseMap();
        }

        public static IntVec3 CellOnAnotherThingMap(this LocalTargetInfo target, Thing another)
        {
            if (target.HasThing)
            {
                return target.Thing.PositionOnAnotherThingMap(another);
            }
            if (another.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                return target.Cell.VehicleMapToOrig(parentVehicle.vehicle);
            }
            return target.Cell;
        }

        public static Rot4 rotForPrint = Rot4.North;

        public const float altitudeOffset = 0.09615385f;

        public const float altitudeOffsetFull = 7.01923085f;

        public static readonly MethodInfo m_OrigToVehicleMap1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_OrigToVehicleMap2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3), typeof(VehiclePawnWithInterior) });

        public static readonly MethodInfo m_VehicleMapToOrig1 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[] { typeof(Vector3) });

        public static readonly MethodInfo m_VehicleMapToOrig2 = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[] { typeof(Vector3), typeof(VehiclePawnWithInterior) });

        public static readonly MethodInfo m_Thing_Map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));

        public static readonly MethodInfo m_BaseMapOfThing = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseMapOfThing));

        public static readonly MethodInfo m_Thing_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));

        public static readonly MethodInfo m_PositionOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnBaseMap));

        public static readonly MethodInfo m_TargetInfo_Cell = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Cell));

        public static readonly MethodInfo m_CellOnBaseMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.CellOnBaseMap));

        public static readonly MethodInfo m_OccupiedRect = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedRect), new Type[] { typeof(Thing) });

        public static readonly MethodInfo m_MovedOccupiedRect = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.MovedOccupiedRect));

        public static readonly MethodInfo m_ToTargetInfo = AccessTools.Method(typeof(LocalTargetInfo), nameof(LocalTargetInfo.ToTargetInfo));

        public static readonly MethodInfo m_ToBaseMapTargetInfo = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.ToBaseMapTargetInfo));
    }
}
