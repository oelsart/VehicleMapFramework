using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI.Group;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

public static class VehicleMapUtility
{
    public static Map CurrentMap
    {
        get
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                return Command_FocusVehicleMap.FocusedVehicle.VehicleMap;
            }
            return Find.CurrentMap;
        }
    }

    public static bool FocusedOnVehicleMap(out VehiclePawnWithMap vehicle)
    {
        if (Command_FocusVehicleMap.FocusedVehicle != null)
        {
            vehicle = Command_FocusVehicleMap.FocusedVehicle;
            return true;
        }
        if (Find.CurrentMap.IsNonFocusedVehicleMapOf(out vehicle))
        {
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVehicleMapOf(this Map map, out VehiclePawnWithMap vehicle)
    {
        if (map == null || !VehicleMapParentsComponent.CachedParentVehicle.TryGetValue(map, out var vehicleLazy))
        {
            vehicle = null;
            return false;
        }
        vehicle = vehicleLazy.Value;
        return vehicle != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNonFocusedVehicleMapOf(this Map map, out VehiclePawnWithMap vehicle)
    {
        if (map.IsVehicleMapOf(out vehicle) && (VehicleMapFramework.settings.drawPlanet || Find.CurrentMap != vehicle.VehicleMap))
        {
            return true;
        }
        vehicle = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnVehicleMapOf(this Thing thing, out VehiclePawnWithMap vehicle)
    {
        if (thing == null)
        {
            vehicle = null;
            return false;
        }
        return thing.Map.IsVehicleMapOf(out vehicle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnNonFocusedVehicleMapOf(this Thing thing, out VehiclePawnWithMap vehicle)
    {
        if (thing == null)
        {
            vehicle = null;
            return false;
        }
        return thing.Map.IsNonFocusedVehicleMapOf(out vehicle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToVehicleMapCoord(this Vector3 original)
    {
        if (Command_FocusVehicleMap.FocusedVehicle != null)
        {
            return VehicleMapUtility.ToVehicleMapCoord(original, Command_FocusVehicleMap.FocusedVehicle);
        }
        if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out var vehicle))
        {
            return VehicleMapUtility.ToVehicleMapCoord(original, vehicle);
        }
        return original;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToVehicleMapCoord(this Vector3 original, VehiclePawnWithMap vehicle)
    {
        var vehicleMapPos = vehicle.cachedDrawPos + VehicleMapUtility.OffsetFor(vehicle);
        var map = vehicle.VehicleMap;
        var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
        var drawPos = (original - vehicleMapPos).RotatedBy(-vehicle.FullRotation.AsAngle) + pivot;
        return drawPos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToVehicleMapCoord(this Vector3 original, VehiclePawnWithMap vehicle, float extraRotation = 0f)
    {
        var vehicleMapPos = vehicle.cachedDrawPos + VehicleMapUtility.OffsetFor(vehicle);
        var map = vehicle.VehicleMap;
        var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
        var drawPos = (original - vehicleMapPos).RotatedBy(-vehicle.FullRotation.AsAngle - extraRotation) + pivot;
        return drawPos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVec3 ToVehicleMapCoord(this IntVec3 original, VehiclePawnWithMap vehicle)
    {
        return original.ToVector3Shifted().ToVehicleMapCoord(vehicle).ToIntVec3();
    }

    public static CellRect ToVehicleMapCoord(this CellRect original)
    {
        var longSide = Mathf.Max(original.Width, original.Height);
        return new CellRect(0, 0, longSide, longSide);
    }

    public static CellRect ClipInsideVehicleMap(ref this CellRect cellRect, Map map)
    {
        if (map.IsVehicleMapOf(out var vehicle))
        {
            var vehicleRect = vehicle.VehicleRect(true);
            cellRect = cellRect.MovedBy(-vehicleRect.Min);
            return cellRect.ClipInsideMap(vehicle.VehicleMap);
        }
        return cellRect.ClipInsideMap(map);
    }

    public static CellRect MovedOccupiedDrawRect(this Thing t)
    {
        Vector2 drawSize = t.DrawSize;
        return GenAdj.OccupiedRect(t.PositionOnBaseMap(), t.BaseRotation(), new IntVec2(Mathf.CeilToInt(drawSize.x), Mathf.CeilToInt(drawSize.y)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToBaseMapCoord(this Vector3 original)
    {
        if (Command_FocusVehicleMap.FocusedVehicle != null)
        {
            return VehicleMapUtility.ToBaseMapCoord(original, Command_FocusVehicleMap.FocusedVehicle).WithY(original.y);
        }
        if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out var vehicle))
        {
            return VehicleMapUtility.ToBaseMapCoord(original, vehicle).WithY(original.y);
        }
        return original;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToBaseMapCoord(this Vector3 original, Map map)
    {
        if (map.IsNonFocusedVehicleMapOf(out var vehicle))
        {
            return VehicleMapUtility.ToBaseMapCoord(original, vehicle);
        }
        return original;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToBaseMapCoord(this Vector3 original, VehiclePawnWithMap vehicle)
    {
        var vehiclePos = vehicle.cachedDrawPos;
        var map = vehicle.VehicleMap;
        var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
        var drawPos = (original - pivot).RotatedBy(vehicle.FullRotation.AsAngle) + vehiclePos;
        drawPos += VehicleMapUtility.OffsetFor(vehicle);
        return drawPos.WithYOffset(VehicleMapUtility.altitudeOffset);
    }

    public static Vector3 ToBaseMapCoord(this Vector3 original, VehiclePawnWithMap vehicle, float extraRotation = 0f)
    {
        var vehiclePos = vehicle.cachedDrawPos;
        var map = vehicle.VehicleMap;
        var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
        var drawPos = (original - pivot).RotatedBy(vehicle.FullRotation.AsAngle + extraRotation) + vehiclePos;
        drawPos += VehicleMapUtility.OffsetFor(vehicle);
        return drawPos.WithYOffset(VehicleMapUtility.altitudeOffset);
    }

    public static Vector3 ToBaseMapCoord(this Vector3 original, VehiclePawnWithMap vehicle, Rot8 rot)
    {
        var vehiclePos = vehicle.cachedDrawPos;
        var map = vehicle.VehicleMap;
        var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
        var drawPos = original.RotatedBy(rot.AsAngle) - pivot.RotatedBy(rot.AsAngle) + vehiclePos;
        drawPos += VehicleMapUtility.OffsetFor(vehicle, rot);
        return drawPos.WithYOffset(VehicleMapUtility.altitudeOffset);
    }

    public static Matrix4x4 ToBaseMapCoord(this Matrix4x4 matrix, VehiclePawnWithMap vehicle)
    {
        var rootPos = matrix.Position();
        matrix.SetColumn(3, rootPos.ToBaseMapCoord(vehicle).WithY(rootPos.y));
        return matrix;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVec3 ToBaseMapCoord(this IntVec3 original)
    {
        return original.ToVector3Shifted().ToBaseMapCoord().ToIntVec3();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVec3 ToBaseMapCoord(this IntVec3 original, VehiclePawnWithMap vehicle)
    {
        return original.ToVector3Shifted().ToBaseMapCoord(vehicle).ToIntVec3();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVec3 ToBaseMapCoord(this IntVec3 original, Map map)
    {
        return original.ToVector3Shifted().ToBaseMapCoord(map).ToIntVec3();
    }

    public static Vector3 ToBaseMapCoord(this IntVec3 original, VehiclePawnWithMap vehicle, Rot8 rot)
    {
        return original.ToVector3Shifted().ToBaseMapCoord(vehicle, rot);
    }

    public static IntVec2 ToHitCell(this IntVec3 cell, VehiclePawnWithMap vehicle)
    {
        var orig = Vector3.zero.ToBaseMapCoord(vehicle).ToVehicleMapCoord(vehicle).ToIntVec3();
        return (orig + cell).ToIntVec2;
    }

    public static Vector3 OffsetFor(VehiclePawnWithMap vehicle)
    {
        return VehicleMapUtility.OffsetFor(vehicle, vehicle.FullRotation);
    }

    public static Vector3 OffsetFor(VehiclePawnWithMap vehicle, Rot8 rot)
    {
        var offset = Vector3.zero;
        VehicleMapProps vehicleMap;
        if ((vehicleMap = vehicle.def.GetModExtension<VehicleMapProps>()) != null)
        {
            switch (rot.AsByte)
            {
                case Rot8.NorthInt:
                    offset = vehicleMap.offsetNorth ?? (vehicleMap.offsetNorth = ((Vector3?)(vehicleMap.offsetSouth ??= vehicleMap.offset)).Value.MirrorVertical()).Value;
                    break;

                case Rot8.EastInt:
                    offset = vehicleMap.offsetEast ?? (vehicleMap.offsetEast = ((Vector3?)(vehicleMap.offsetWest ??= vehicleMap.offset)).Value.MirrorHorizontal()).Value;
                    break;

                case Rot8.SouthInt:
                    offset = vehicleMap.offsetSouth ?? (vehicleMap.offsetSouth = ((Vector3?)(vehicleMap.offsetNorth ??= vehicleMap.offset)).Value.MirrorVertical()).Value;
                    break;

                case Rot8.WestInt:
                    offset = vehicleMap.offsetWest ?? (vehicleMap.offsetWest = ((Vector3?)(vehicleMap.offsetEast ??= vehicleMap.offset)).Value.MirrorHorizontal()).Value;
                    break;

                case Rot8.NorthEastInt:
                    offset = vehicleMap.offsetNorthEast ?? (vehicleMap.offsetNorthEast = ((Vector3?)(vehicleMap.offsetNorthWest ??= ((Vector3?)(vehicleMap.offsetNorth ??= vehicleMap.offset.RotatedBy(rot.AsAngle))).Value.RotatedBy(-45f))).Value.MirrorHorizontal()).Value;
                    break;

                case Rot8.SouthEastInt:
                    offset = vehicleMap.offsetSouthEast ?? (vehicleMap.offsetSouthEast = ((Vector3?)(vehicleMap.offsetSouthWest ??= ((Vector3?)(vehicleMap.offsetSouth ??= vehicleMap.offset.RotatedBy(rot.AsAngle))).Value.RotatedBy(45f))).Value.MirrorHorizontal()).Value;
                    break;

                case Rot8.SouthWestInt:
                    offset = vehicleMap.offsetSouthWest ?? (vehicleMap.offsetSouthWest = ((Vector3?)(vehicleMap.offsetSouthEast ??= ((Vector3?)(vehicleMap.offsetSouth ??= vehicleMap.offset.RotatedBy(rot.AsAngle))).Value.RotatedBy(-45f))).Value.MirrorHorizontal()).Value;
                    break;

                case Rot8.NorthWestInt:
                    offset = vehicleMap.offsetNorthWest ?? (vehicleMap.offsetNorthWest = ((Vector3?)(vehicleMap.offsetNorthEast ??= ((Vector3?)(vehicleMap.offsetNorth ??= vehicleMap.offset.RotatedBy(rot.AsAngle))).Value.RotatedBy(45f))).Value.MirrorHorizontal()).Value;
                    break;

                default: break;
            }
        }
        return offset;
    }

    public static List<Type> SelectSectionLayers(List<Type> subClasses, Map map)
    {
        var excepts = new HashSet<Type>();
        if (map?.Parent is MapParent_Vehicle)
        {
            excepts.AddRange(new Type[] { typeof(SectionLayer_ThingsGeneral), t_SectionLayer_Terrain, typeof(SectionLayer_ThingsPowerGrid) });
            if (VFECore.Active)
            {
                excepts.Add(AccessTools.TypeByName("PipeSystem.SectionLayer_Resource"));
            }
            if (!DubsBadHygiene.Active || DubsBadHygiene.LiteMode)
            {
                excepts.Add(typeof(SectionLayer_ThingsSewagePipeOnVehicle));
            }
            if (!Rimefeller.Active)
            {
                excepts.Add(typeof(SectionLayer_ThingsPipeOnVehicle));
            }
            return [.. subClasses.Except(excepts)];
        }
        excepts.AddRange(new Type[] { typeof(SectionLayer_ThingsGeneralOnVehicle), typeof(SectionLayer_TerrainOnVehicle), typeof(SectionLayer_LightingOnVehicle), typeof(SectionLayer_ThingsPowerGridOnVehicle) });
        if (VFECore.Active)
        {
            excepts.Add(AccessTools.TypeByName("VehicleMapFramework.SectionLayer_ResourceOnVehicle"));
        }
        excepts.Add(typeof(SectionLayer_ThingsSewagePipeOnVehicle));
        excepts.Add(typeof(SectionLayer_ThingsPipeOnVehicle));
        return [.. subClasses.Except(excepts)];
    }

    private static readonly Type t_SectionLayer_Terrain = AccessTools.TypeByName("Verse.SectionLayer_Terrain");

    public static Rot4 RotationForPrint(this Thing thing)
    {
        var rot = thing.Rotation;

        bool SameMaterialByRot()
        {
            var graphic = thing.Graphic;
            var rotation = new Rot4(rot.AsInt + VehicleMapUtility.rotForPrint.AsInt);
            return graphic != null && graphic.MatAt(rot, thing) == graphic.MatAt(rotation, thing) && graphic.DrawOffset(rot) == graphic.DrawOffset(rotation);
        }

        if (VehicleMapUtility.rotForPrint != Rot4.North && (thing.def.size.x != thing.def.size.z || ((thing.def.rotatable || (thing.def.graphicData?.drawRotated ?? false)) && thing.Graphic is Graphic_Multi && !SameMaterialByRot())))
        {
            rot.AsInt += VehicleMapUtility.rotForPrint.AsInt;
        }
        return rot;
    }

    public static float PrintExtraRotation(Thing thing)
    {
        float result = 0f;
        if (thing.IsOnVehicleMapOf(out _))
        {
            result -= VehicleMapUtility.rotForPrint.AsAngle;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Map BaseMap(this Map map)
    {
        if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            return vehicle.Map;
        }
        return map;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Map BaseMap(this Thing thing)
    {
        if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            return vehicle.Map;
        }
        return thing.Map;
    }

    public static Map BaseMap(this Zone zone)
    {
        if (zone.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            return vehicle.Map;
        }
        return zone.Map;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVec3 PositionOnBaseMap(this Thing thing)
    {
        if (thing.IsOnVehicleMapOf(out var vehicle))
        {
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(thing.Map);
            if (component.cachedPosOnBaseMap.TryGetValue(thing, out var pos))
            {
                return pos;
            }
            pos = thing.Position.ToBaseMapCoord(vehicle);
            component.cachedPosOnBaseMap[thing] = pos;
            return pos;
        }
        return thing.Position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVec3 PositionOnBaseMap(this IHaulDestination dest)
    {
        if (dest.Map.IsVehicleMapOf(out var vehicle))
        {
            return dest.Position.ToBaseMapCoord(vehicle);
        }
        return dest.Position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                rootPosition = thing2.PositionOnBaseMap();
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

    public static IntVec3 ToThingMapCoord(this IntVec3 origin, Thing thing)
    {
        if (thing.IsOnVehicleMapOf(out var vehicle))
        {
            return origin.ToVehicleMapCoord(vehicle);
        }
        return origin;
    }

    public static IntVec3 ToThingBaseMapCoord(this IntVec3 origin, Thing thing)
    {
        if (thing.IsOnVehicleMapOf(out var vehicle))
        {
            return origin.ToBaseMapCoord(vehicle);
        }
        return origin;
    }

    public static Vector3 ToThingBaseMapCoord(this Vector3 origin, Thing thing)
    {
        if (thing.IsOnVehicleMapOf(out var vehicle))
        {
            return origin.ToBaseMapCoord(vehicle);
        }
        return origin;
    }

    public static IntVec3 CellOnBaseMap(this ref LocalTargetInfo target)
    {
        if (target.HasThing)
        {
            if (target.Thing.IsOnVehicleMapOf(out var vehicle))
            {
                var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(target.Thing.Map);
                if (component.cachedPosOnBaseMap.TryGetValue(target.Thing, out var pos))
                {
                    return pos;
                }
                pos = target.Thing.Position.ToBaseMapCoord(vehicle);
                component.cachedPosOnBaseMap[target.Thing] = pos;
                return pos;
            }
        }
        return target.Cell;
    }

    public static IntVec3 CellOnBaseMap(this ref TargetInfo target)
    {
        if (target.Map.IsVehicleMapOf(out var vehicle))
        {
            return target.Cell.ToBaseMapCoord(vehicle);
        }
        return target.Cell;
    }

    public static CellRect MovedOccupiedRect(this Thing thing)
    {
        var size = thing.def.size;
        return GenAdj.OccupiedRect(thing.PositionOnBaseMap(), thing.BaseRotation(), new IntVec2(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.z)));
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
            return thing.PositionOnBaseMap().ToVehicleMapCoord(vehicle);
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
            return target.Cell.ToVehicleMapCoord(vehicle);
        }
        return target.Cell;
    }

    public static IntVec3 CellOnAnotherMap(this IntVec3 cell, Map another)
    {
        if (another.IsVehicleMapOf(out var vehicle))
        {
            return cell.ToVehicleMapCoord(vehicle);
        }
        return cell;
    }

    public static Rot4 BaseRotation(this Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            return new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt);
        }
        return thing.Rotation;
    }

    public static Rot4 BaseRotationVehicleDraw(this Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            return new Rot4(thing.Rotation.AsInt + vehicle.FullRotation.RotForVehicleDraw().AsInt);
        }
        return thing.Rotation;
    }

    public static Rot8 BaseFullRotation(this VehiclePawn vehicle)
    {
        if (!vehicle.VehicleDef.graphicData.drawRotated)
        {
            return Rot8.North;
        }
        var rot = new Rot8(vehicle.Rotation, vehicle.Angle);
        if (vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle2))
        {
            rot = new Rot8(Rot8.FromIntClockwise((rot.AsIntClockwise + vehicle2.FullRotation.AsIntClockwise) % 8));
        }
        return rot;
    }

    public static Rot8 BaseFullRotation(this Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            return new Rot8(Rot8.FromIntClockwise((new Rot8(thing.Rotation).AsIntClockwise + vehicle.FullRotation.AsIntClockwise) % 8));
        }
        return thing.Rotation;
    }

    public static Rot4 BaseFullRotationAsRot4(this Thing thing)
    {
        Rot4 rot = default;
        Rot8Utility.rotInt(ref rot) = thing.BaseFullRotation().AsByte;
        return rot;
    }

    public static Rot8 BaseFullRotationDoor(this Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            var rot = new Rot8(Rot8.FromIntClockwise((new Rot8(thing.Rotation).AsIntClockwise + vehicle.FullRotation.AsIntClockwise) % 8));
            return rot.FacingCell.z < 0 ? rot.Opposite : rot;
        }
        return thing.Rotation;
    }

    public static Rot4 DirectionToInsideMap(this IntVec3 c, VehiclePawnWithMap vehicle)
    {
        Rot4 dir;
        var map = vehicle.VehicleMap;
        if (c.x == 0 || vehicle.CachedOutOfBoundsCells.Contains(new IntVec3(c.x - 1, c.y, c.z))) dir = Rot4.East;
        else if (c.x == map.Size.x - 1 || vehicle.CachedOutOfBoundsCells.Contains(new IntVec3(c.x + 1, c.y, c.z))) dir = Rot4.West;
        else if (c.z == 0 || vehicle.CachedOutOfBoundsCells.Contains(new IntVec3(c.x, c.y, c.z - 1))) dir = Rot4.North;
        else if (c.z == map.Size.z - 1 || vehicle.CachedOutOfBoundsCells.Contains(new IntVec3(c.x, c.y, c.z + 1))) dir = Rot4.South;
        else
        {
            Log.ErrorOnce("That position is not the edge of the map", 494896165);
            return Rot4.Invalid;
        }
        return dir;
    }

    public static Rot8 BaseFullDirectionToInsideMap(this IntVec3 c, VehiclePawnWithMap vehicle)
    {
        var dir = c.DirectionToInsideMap(vehicle);
        var map = vehicle.VehicleMap;
        if (Find.CurrentMap != map)
        {
            return new Rot8(Rot8.FromIntClockwise((new Rot8(dir).AsIntClockwise + vehicle.FullRotation.AsIntClockwise) % 8));
        }
        return dir;
    }

    public static Rot8 FullDirectionToInsideMap(this IntVec3 c, Map map)
    {
        Rot8 dir;
        if (c.x == 0)
        {
            if (c.z == 0)
            {
                dir = Rot8.NorthEast;
            }
            else if (c.z == map.Size.z - 1)
            {
                dir = Rot8.SouthEast;
            }
            else
            {
                dir = Rot8.East;
            }
        }
        else if (c.x == map.Size.x - 1)
        {
            if (c.z == 0)
            {
                dir = Rot8.NorthWest;
            }
            else if (c.z == map.Size.z - 1)
            {
                dir = Rot8.SouthWest;
            }
            else
            {
                dir = Rot8.West;
            }
        }
        else if (c.z == 0) dir = Rot8.North;
        else if (c.z == map.Size.z - 1) dir = Rot8.South;
        else
        {
            Log.ErrorOnce("That position is not the edge of the map", 494896165);
            return Rot8.Invalid;
        }
        return dir;
    }

    public static int HalfLength(this VehiclePawn vehicle)
    {
        return vehicle.VehicleDef.HalfLength();
    }

    public static int HalfLength(this VehicleDef vehicleDef)
    {
        return vehicleDef.size.z / 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetDrawPos(this Thing thing, ref Vector3 result)
    {
        var map = thing.Map;
        if (map.IsNonFocusedVehicleMapOf(out var vehicle))
        {
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
            if (!component.cacheMode)
            {
                if (!component.cachedDrawPos.TryGetValue(thing, out result))
                {
                    component.cacheMode = true;
                    try
                    {
                        result = thing.DrawPos.ToBaseMapCoord(vehicle);
                        component.cachedDrawPos[thing] = result;
                    }
                    finally
                    {
                        component.cacheMode = false;
                    }
                }
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetFullRotation(this VehiclePawn vehicle, ref Rot8 rot)
    {
        var map = vehicle.Map;
        if (map != null)
        {
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
            if (!component.cachedFullRot.TryGetValue(vehicle, out rot))
            {
                rot = vehicle.BaseFullRotation();
                component.cachedFullRot[vehicle] = rot;
            }
            return true;
        }
        return false;
    }

    public static Map MapHeldBaseMap(this Thing thing)
    {
        return thing.MapHeld.BaseMap();
    }

    public static Rot4 RotForVehicleDraw(this Rot8 rot)
    {
        if (rot.IsDiagonal)
        {
            return rot == Rot8.NorthEast || rot == Rot8.NorthWest ? Rot4.North : Rot4.South;
        }
        return rot;
    }

    public static float VehicleMapMass(VehiclePawnWithMap vehicle)
    {
        return CollectionsMassCalculator.MassUsage(vehicle.VehicleMap.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true);
    }

    public static Vector3 RotateForPrintNegate(Vector3 vector)
    {
        return vector.RotatedBy(-VehicleMapUtility.rotForPrint.AsAngle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetVehicleMap(this Vector3 point, Map map, out VehiclePawnWithMap vehicle, bool getStructureCell = true)
    {
        if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out vehicle))
        {
            return true;
        }

        if (map == null)
        {
            vehicle = null;
            return false;
        }

        var vehicles = VehiclePawnWithMapCache.AllVehiclesOn(map);
        vehicle = vehicles.FirstOrDefault(v =>
        {
            var rect = new Rect(0f, 0f, v.VehicleMap.Size.x, v.VehicleMap.Size.z).ContractedBy(0.9f);
            var vector = point.ToVehicleMapCoord(v);
            var intVec = vector.ToIntVec3();
            return rect.Contains(new Vector2(vector.x, vector.z)) && !v.CachedOutOfBoundsCells.Contains(intVec) && (getStructureCell || !v.CachedStructureCells.Contains(intVec));
        });
        return vehicle != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetVehicleMap(this IntVec3 c, Map map, out VehiclePawnWithMap vehicle)
    {
        vehicle = MapComponentCache<VehicleMapGrid>.GetComponent(map).VehicleAt(c);
        return vehicle != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<Map> BaseMapAndVehicleMaps(this Map map)
    {
        var baseMap = map.BaseMap();
        if (baseMap == null)
        {
            yield break;
        }
        yield return baseMap;

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(baseMap))
        {
            if (vehicle.VehicleMap != null)
            {
                yield return vehicle.VehicleMap;
            }
        }
    }

    public static void VirtualMapTransfer(this Thing thing, Map map)
    {
        mapIndexOrState(thing) = (sbyte)map.Index;
    }

    public static void VirtualMapTransfer(this Thing thing, Map map, IntVec3 c)
    {
        mapIndexOrState(thing) = (sbyte)map.Index;
        thing.SetPositionDirect(c);
    }

    private static readonly AccessTools.FieldRef<Thing, sbyte> mapIndexOrState = AccessTools.FieldRefAccess<Thing, sbyte>("mapIndexOrState");

    //thingが車両マップ上にあったらthingの中心を基準として位置と回転を下の車両基準に回転するわよ
    public static void SetTRSOnVehicle(ref Matrix4x4 matrix, Vector3 pos, Quaternion q, Vector3 s, Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            var rot = vehicle.FullRotation;
            var angle = rot.AsAngle;
            matrix = Matrix4x4.TRS(Ext_Math.RotatePoint(pos, thing.TrueCenter(), -angle), q * rot.AsQuat(), s);
            return;
        }
        matrix = Matrix4x4.TRS(pos, q, s);
    }

    public static Vector3 FocusedDrawPosOffset(Vector3 original, IntVec3 center)
    {
        Thing thing;
        if ((thing = Find.Selector.SelectedObjects.OfType<Thing>().FirstOrDefault(t => t.Position == center)) != null)
        {
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
            }
        }
        else if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle))
        {
            return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
        }
        return original;
    }

    public static Vector3 SelectedDrawPosOffset(Vector3 original, IntVec3 center)
    {
        VehiclePawnWithMap vehicle = null;
        if (Find.Selector.SelectedObjects.Any(o => o is Thing thing && thing.Position == center && thing.IsOnNonFocusedVehicleMapOf(out vehicle)))
        {
            return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
        }
        return original;
    }

    public static IEnumerable<Thing> ColonyThingsWillingToBuyOnVehicle(this VehiclePawnWithMap vehicle, ITrader trader)
    {
        var map = vehicle.VehicleMap;
        IEnumerable<Thing> enumerable = map.listerThings.AllThings.Where(x => x.def.category == ThingCategory.Item && TradeUtility.PlayerSellableNow(x, trader) && !x.Position.Fogged(x.Map) && (map.areaManager.Home[x.Position] || x.IsInAnyStorage()));
        foreach (Thing item in enumerable)
        {
            yield return item;
        }

        if (ModsConfig.BiotechActive)
        {
            List<Building> list = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.GeneBank);
            foreach (Building item2 in list)
            {
                CompGenepackContainer compGenepackContainer = item2.TryGetComp<CompGenepackContainer>();
                if (compGenepackContainer == null)
                {
                    continue;
                }

                List<Genepack> containedGenepacks = compGenepackContainer.ContainedGenepacks;
                foreach (Genepack item3 in containedGenepacks)
                {
                    yield return item3;
                }
            }
        }

        IEnumerable<IHaulSource> enumerable2 = map.listerBuildings.AllColonistBuildingsOfType<IHaulSource>();
        foreach (IHaulSource item4 in enumerable2)
        {
            Building thing = (Building)item4;

            foreach (Thing item5 in item4.GetDirectlyHeldThings())
            {
                yield return item5;
            }
        }

        if (trader is Pawn pawn && pawn.GetLord() == null)
        {
            yield break;
        }

        foreach (Pawn item6 in from x in TradeUtility.AllSellableColonyPawns(map)
                               where !x.Downed
                               select x)
        {
            yield return item6;
        }
    }

    public static bool ShouldRotatedOnVehicle(this ThingDef tDef)
    {
        return tDef.fillPercent > 0.25f ||
            tDef.Size != IntVec2.One ||
            (tDef.graphic is not Graphic_Single && tDef.graphic is not Graphic_Collection) ||
            tDef.hasInteractionCell ||
            tDef.drawerType == DrawerType.MapMeshOnly ||
            tDef.drawerType == DrawerType.MapMeshAndRealTime ||
            tDef.size.x != tDef.size.z;
    }

    public static List<Thing> GetThingListAcrossMaps(this IntVec3 c, Map map)
    {
        tmpList.Clear();
        var orig = map.IsVehicleMapOf(out var vehicle) ? c.ToBaseMapCoord(vehicle) : c;
        foreach (var m in map.BaseMapAndVehicleMaps())
        {
            if (m.IsVehicleMapOf(out var vehicle2))
            {
                var c2 = orig.ToVehicleMapCoord(vehicle2);
                tmpList.AddRange(m.thingGrid.ThingsAt(c2));
            }
            else
            {
                tmpList.AddRange(m.thingGrid.ThingsAt(orig));
            }
        }
        return tmpList;
    }

    private static List<Thing> tmpList = [];

    public static Pawn GetFirstPawnAcrossMaps(this IntVec3 c, Map map)
    {
        List<Thing> thingList = c.GetThingListAcrossMaps(map);
        for (int i = 0; i < thingList.Count; i++)
        {
            if (thingList[i] is Pawn result)
            {
                return result;
            }
        }

        return null;
    }

    public static Thing GetCoverOnThingMap(this IntVec3 c, Map map, Thing thing)
    {
        var thingMap = thing?.MapHeld;
        if (thingMap != null)
        {
            var c2 = c.ToBaseMapCoord(thingMap);
            if (c2.InBounds(thingMap))
            {
                return c2.GetCover(thingMap);
            }
        }
        return c.GetCover(map);
    }

    public static Rot4 rotForPrint = Rot4.North;

    public const float altitudeOffset = 0.09615385f;

    public const float altitudeOffsetFull = 7.692308f;
}