using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public static class VehicleMapUtility
    {
        public static Map CurrentMap
        {
            get
            {
                if (Command_FocusVehicleMap.FocusedVehicle != null)
                {
                    return Command_FocusVehicleMap.FocusedVehicle.interiorMap;
                }
                return Find.CurrentMap;
            }
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
            if (thing == null)
            {
                vehicle = null;
                return false;
            }
            return thing.Map.IsVehicleMapOf(out vehicle);
        }

        public static Vector3 VehicleMapToOrig(this Vector3 original)
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                return VehicleMapUtility.VehicleMapToOrig(original, Command_FocusVehicleMap.FocusedVehicle);
            }
            return original;
        }

        public static Vector3 VehicleMapToOrig(this Vector3 original, VehiclePawnWithInterior vehicle)
        {
            var vehicleMapPos = vehicle.cachedDrawPos + VehicleMapUtility.OffsetOf(vehicle);
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - vehicleMapPos.WithY(0f)).RotatedBy(-vehicle.FullRotation.AsAngle) + pivot;
            return drawPos.WithYOffset(-VehicleMapUtility.altitudeOffsetFull);
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

        public static CellRect ClipInsideVehicleMap(ref this CellRect cellRect, Map map)
        {
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                var clip = cellRect.ClipInsideRect(vehicle.VehicleRect(true));
                return cellRect = clip.MovedBy(-clip.Min);
            }
            return cellRect.ClipInsideMap(map);
        }

        public static Vector3 OrigToVehicleMap(this Vector3 original)
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                var vehicle = Command_FocusVehicleMap.FocusedVehicle;
                return VehicleMapUtility.OrigToVehicleMap(original, vehicle);
            }
            return original;
        }

        public static Vector3 OrigToVehicleMap(this Vector3 original, VehiclePawnWithInterior vehicle)
        {
            var vehiclePos = vehicle.cachedDrawPos.WithY(0f);
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - pivot).RotatedBy(vehicle.FullRotation.AsAngle) + vehiclePos;
            drawPos += VehicleMapUtility.OffsetOf(vehicle);
            return drawPos.WithYOffset(VehicleMapUtility.altitudeOffsetFull);
        }

        public static Vector3 OrigToVehicleMap(this Vector3 original, VehiclePawnWithInterior vehicle, Rot8 rot)
        {
            var vehiclePos = vehicle.cachedDrawPos.WithY(0f);
            var map = vehicle.interiorMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = original.RotatedBy(rot.AsAngle) - pivot.RotatedBy(rot.AsAngle) + vehiclePos;
            drawPos += VehicleMapUtility.OffsetOf(vehicle, rot);
            return drawPos.WithYOffset(VehicleMapUtility.altitudeOffsetFull);
        }

        public static IntVec3 OrigToVehicleMap(this IntVec3 original, VehiclePawnWithInterior vehicle)
        {
            return original.ToVector3Shifted().OrigToVehicleMap(vehicle).ToIntVec3();
        }
        public static Vector3 OrigToVehicleMap(this IntVec3 original, VehiclePawnWithInterior vehicle, Rot8 rot)
        {
            return original.ToVector3Shifted().OrigToVehicleMap(vehicle, rot);
        }

        public static Vector3 OffsetOf(VehiclePawnWithInterior vehicle)
        {
            return VehicleMapUtility.OffsetOf(vehicle, vehicle.FullRotation);
        }

        public static Vector3 OffsetOf(VehiclePawnWithInterior vehicle, Rot8 rot)
        {
            var offset = Vector3.zero;
            VehicleMapProps vehicleMap;
            if ((vehicleMap = vehicle.def.GetModExtension<VehicleMapProps>()) != null)
            {
                switch (rot.AsByte)
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
            if (map?.Parent is MapParent_Vehicle)
            {
                return subClasses.Except(new Type[] { typeof(SectionLayer_ThingsGeneral), t_SectionLayer_Terrain, typeof(SectionLayer_LightingOverlay) }).ToList();
            }
            return subClasses.Except(new Type[] { typeof(SectionLayer_ThingsOnVehicle), typeof(SectionLayer_TerrainOnVehicle), typeof(SectionLayer_LightingOnVehicle) }).ToList();
        }

        private static Type t_SectionLayer_Terrain = AccessTools.TypeByName("Verse.SectionLayer_Terrain");

        public static float PrintExtraRotation(Thing thing)
        {
            float result = 0f;
            if (thing?.Map?.Parent is MapParent_Vehicle)
            {
                result -= VehicleMapUtility.rotForPrint.AsAngle * (thing.Graphic is Graphic_Single ? 2f : 1f);
            }
            return result;
        }

        public static Map BaseMapOfThing(this Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return vehicle.Map;
            }
            return thing.Map;
        }

        public static IntVec3 PositionOnBaseMap(this Thing thing)
        {
            if (VehiclePawnWithMapCache.cachedPosOnBaseMap.TryGetValue(thing, out var pos))
            {
                return pos;
            }
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                pos = thing.Position.OrigToVehicleMap(vehicle);
                VehiclePawnWithMapCache.cachedPosOnBaseMap[thing] = pos;
                return pos;
            }
            return thing.Position;
        }

        public static IntVec3 PositionOnBaseMap(this IHaulDestination dest)
        {
            if (dest.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return dest.Position.OrigToVehicleMap(vehicle);
            }
            return dest.Position;
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
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return origin.VehicleMapToOrig(vehicle);
            }
            return origin;
        }

        public static IntVec3 OrigToThingMap(this IntVec3 origin, Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return origin.OrigToVehicleMap(vehicle);
            }
            return origin;
        }

        public static IntVec3 CellOnBaseMap(this ref LocalTargetInfo target)
        {
            if (target.HasThing) {
                if (VehiclePawnWithMapCache.cachedPosOnBaseMap.TryGetValue(target.Thing, out var pos))
                {
                    return pos;
                }
                if (target.Thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
                {
                    pos = target.Thing.Position.OrigToVehicleMap(vehicle);
                    VehiclePawnWithMapCache.cachedPosOnBaseMap[target.Thing] = pos;
                    return pos;
                }
            }
            return target.Cell;
        }

        public static CellRect MovedOccupiedRect(this Thing thing)
        {
            var drawSize = thing.DrawSize;
            return GenAdj.OccupiedRect(thing.PositionOnBaseMap(), thing.BaseRotationOfThing(), new IntVec2(Mathf.CeilToInt(drawSize.x), Mathf.CeilToInt(drawSize.y)));
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
            if (another.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
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
            if (another.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return target.Cell.VehicleMapToOrig(vehicle);
            }
            return target.Cell;
        }

        public static IntVec3 CellOnAnotherMap(this IntVec3 cell, Map another)
        {
            if (another.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return cell.VehicleMapToOrig(vehicle);
            }
            return cell;
        }

        public static Rot4 BaseRotationOfThing(this Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt);
            }
            return thing.Rotation;
        }

        public static Rot8 BaseFullRotationOfThing(this Thing thing)
        {
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                var rot = new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt);
                return new Rot8(rot, rot.IsHorizontal ? vehicle.Angle : 0f);
            }
            return thing.Rotation;
        }

        public static bool TryGetOnVehicleDrawPos(this Thing thing, ref Vector3 result)
        {
            if (VehiclePawnWithMapCache.cachedDrawPos.TryGetValue(thing, out var pos))
            {
                result = pos;
                return true;
            }
            if (!VehiclePawnWithMapCache.cacheMode && thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                VehiclePawnWithMapCache.cacheMode = true;
                var drawPos = thing.DrawPos;
                VehiclePawnWithMapCache.cachedDrawPos[thing] = drawPos.OrigToVehicleMap(vehicle);
                VehiclePawnWithMapCache.cacheMode = false;
                result = VehiclePawnWithMapCache.cachedDrawPos[thing];
                return true;
            }
            return false;
        }

        public static Map MapHeldBaseMap(this Thing thing)
        {
            var map = thing.MapHeld;
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return vehicle.Map;
            }
            return map;
        }

        public static Map BaseMap(this Map map)
        {
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return vehicle.Map;
            }
            return map;
        }

        public static Rot4 RotForVehicleDraw(this Rot8 rot)
        {
            if (rot.IsDiagonal)
            {
                return rot == Rot8.NorthEast || rot == Rot8.NorthWest ? Rot4.North : Rot4.South;
            }
            return rot;
        }

        public static Rot4 rotForPrint = Rot4.North;

        public const float altitudeOffset = 0.09615385f;

        public const float altitudeOffsetFull = 7.692308f;
    }
}
