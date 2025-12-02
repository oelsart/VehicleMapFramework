using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI.Group;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

public static class VehicleMapUtility
{
    public const float YCompress = 40f;

    private const float AltitudeOffset = 0.09615385f;

    private const float AltitudeOffsetFull = 7.692308f;
    
    public static Map CurrentMap =>
        Command_FocusVehicleMap.FocusedVehicle != null
            ? Command_FocusVehicleMap.FocusedVehicle.CurrentLevel : Find.CurrentMap;

    public static Rot4 RotForPrintCounter => RotForPrint.IsHorizontal ? RotForPrint.Opposite : RotForPrint;

    public static Rot4 RotForPrint { get; set; }

    public static bool FocusedOnVehicleMap(out VehiclePawnWithMap vehicle)
    {
        if (Command_FocusVehicleMap.FocusedVehicle is null)
            return Find.CurrentMap.IsNonFocusedVehicleMapOf(out vehicle);
        vehicle = Command_FocusVehicleMap.FocusedVehicle;
        return true;
    }

    extension(Map map)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsVehicleMapOf(out VehiclePawnWithMap vehicle)
        {
            if (map?.Parent is PocketMapParent pocketMapParent)
            {
                if (pocketMapParent is MapParent_Vehicle mapParentVehicle)
                {
                    vehicle = mapParentVehicle.vehicle;
                    return vehicle != null;
                }
                if (pocketMapParent.sourceMap?.Parent is MapParent_Vehicle mapParentVehicle2)
                {
                    vehicle = mapParentVehicle2.vehicle;
                    return vehicle != null;
                }
            }
            vehicle = null;
            return false;
        }

        public bool IsVehicleMap => map.IsVehicleMapOf(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNonFocusedVehicleMapOf(out VehiclePawnWithMap vehicle)
        {
            if (map.IsVehicleMapOf(out vehicle) && (VehicleMapFramework.settings.drawPlanet || Find.CurrentMap != vehicle.VehicleMap))
            {
                return true;
            }
            vehicle = null;
            return false;
        }

        public bool IsNonFocusedVehicleMap => map.IsNonFocusedVehicleMapOf(out _);

        public IEnumerable<Map> BaseMapAndVehicleMaps()
        {
            if (MultiFloors.Active && MultiFloors.GroundMap(map) != map)
            {
                yield return map;
                yield break;
            }
            var baseMap = map.BaseMap();
            if (baseMap == null)
            {
                yield break;
            }
            yield return baseMap;

            if (baseMap.IsVehicleMapOf(out var vehicle) && vehicle.GetVehicleCaravan() is { } vehicleCaravan)
            {
                foreach (var vehicle2 in vehicleCaravan.Vehicles.OfType<VehiclePawnWithMap>())
                    yield return vehicle2.VehicleMap;
            }
            else
            {
                foreach (var vehicle2 in VehiclePawnWithMapCache.AllVehiclesOn(baseMap))
                {
                    if (vehicle2.VehicleMap != null)
                    {
                        yield return vehicle2.VehicleMap;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Map BaseMap()
        {
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return vehicle.Map;
            }
            return map;
        }

        public Map GroundMap => map.BaseMap();
        
        public object BaseMapOrCaravan =>
            map.IsVehicleMapOf(out var vehicle)
                ? vehicle.Spawned ? vehicle.Map : vehicle.GetVehicleCaravan()
                : map;
    }

    extension(Thing thing)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOnVehicleMapOf(out VehiclePawnWithMap vehicle)
        {
            if (thing != null) return thing.Map.IsVehicleMapOf(out vehicle);
            vehicle = null;
            return false;
        }

        public bool IsOnVehicleMap => thing.IsOnVehicleMapOf(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOnNonFocusedVehicleMapOf(out VehiclePawnWithMap vehicle)
        {
            if (thing != null) return thing.Map.IsNonFocusedVehicleMapOf(out vehicle);
            vehicle = null;
            return false;
        }

        public bool IsOnNonFocusedVehicleMap => thing.IsOnNonFocusedVehicleMapOf(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Map BaseMap()
        {
            if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return vehicle.Map;
            }
            return thing.Map;
        }

        public Map GroundMap => thing.BaseMap();

        public object BaseMapOrCaravan =>
            thing.IsOnVehicleMapOf(out var vehicle)
                ? vehicle.Spawned ? vehicle.Map : vehicle.GetVehicleCaravan()
                : thing.Map;

        public Map MapHeldBaseMap()
        {
            return thing.MapHeld.BaseMap();
        }

        public object MapHeldBaseMapOrCaravan
        {
            get
            {
                var mapHeld = thing.MapHeld;
                return mapHeld.IsVehicleMapOf(out var vehicle)
                    ? vehicle.Spawned ? vehicle.Map : vehicle.GetVehicleCaravan()
                    : mapHeld;
            }
        }
        
        public bool TryGetDrawPos(ref Vector3 result)
        {
            if (VehiclePawnWithMapCache.cacheModeGlobal) return false;

            var map = thing.Map;
            if (map.IsNonFocusedVehicleMapOf(out var vehicle))
            {
                var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
                if (!component.cacheMode)
                {
                    if (!component.cachedDrawPos.TryGetValue(thing, out result))
                    {
                        VehiclePawnWithMapCache.cacheModeGlobal = true;
                        try
                        {
                            result = thing.DrawPos.ToBaseMapCoord(vehicle);
                            component.cachedDrawPos[thing] = result;
                        }
                        finally
                        {
                            VehiclePawnWithMapCache.cacheModeGlobal = false;
                        }
                    }
                    return true;
                }
                VehiclePawnWithMapCache.cacheModeGlobal = true;
                try
                {
                    if (thing.def.category == ThingCategory.Item &&
                        thing.GetSlotGroup()?.parent is Building_Hatch)
                    {
                        result = Vector3.negativeInfinity;
                    }
                    else
                    {
                        result = thing.DrawPos;
                    }
                }
                finally
                {
                    VehiclePawnWithMapCache.cacheModeGlobal = false;
                }
                return true;
            }
            return false;
        }
    }

    extension(float original)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float YOffsetFull()
        {
            return original / YCompress + AltitudeOffsetFull;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float YOffsetFull(VehiclePawnWithMap vehicle)
        {
            return original / YCompress + vehicle.cachedDrawPos.y;
        }

        public float FlipAngle(VehiclePawn vehicle)
        {
            return vehicle.Graphic.WestFlipped && vehicle.BaseRotation() == Rot4.West ? -original : original;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float YOffset()
        {
            return original / YCompress + AltitudeOffset;
        }
    }

    extension(Vector3 original)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 YOffset()
        {
            return original.WithY(original.y.YOffset());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 YOffsetFull()
        {
            return original.WithY(original.y.YOffsetFull());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 YOffsetFull(VehiclePawnWithMap vehicle)
        {
            return original.WithY(original.y.YOffsetFull(vehicle));
        }

        public Vector3 ToVehicleMapCoord()
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                return original.ToVehicleMapCoord(Command_FocusVehicleMap.FocusedVehicle);
            }
            if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out _) &&
                UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                return original.ToVehicleMapCoord(vehicle);
            }
            return original;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToVehicleMapCoord(VehiclePawnWithMap vehicle)
        {
            var vehicleMapPos = vehicle.cachedDrawPos + OffsetFor(vehicle);
            var map = vehicle.VehicleMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original - vehicleMapPos).RotatedBy(-vehicle.FullAngle()) + pivot;
            return drawPos;
        }
        
        public Vector3 ToNonFocusedThingMapCoord(Thing thing)
        {
            return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) ? original.ToVehicleMapCoord(vehicle) : original;
        }
        
        public Vector3 ToBaseMapCoord()
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                return ToBaseMapCoord(original, Command_FocusVehicleMap.FocusedVehicle).WithY(original.y);
            }
            if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out _) &&
                UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                return ToBaseMapCoord(original, vehicle).WithY(original.y);
            }
            return original;
        }

        public Vector3 ToBaseMapCoord(Map map)
        {
            return map.IsNonFocusedVehicleMapOf(out var vehicle) ? ToBaseMapCoord(original, vehicle) : original;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToBaseMapCoord(VehiclePawnWithMap vehicle)
        {
            var vehiclePos = vehicle.cachedDrawPos;
            var map = vehicle.VehicleMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original.YOffset() - pivot).RotatedBy(vehicle.FullAngle()) + vehiclePos;
            drawPos += OffsetFor(vehicle);
            return drawPos;
        }

        public Vector3 ToBaseMapCoord(VehiclePawnWithMap vehicle, Rot8 rot)
        {
            var vehiclePos = vehicle.cachedDrawPos;
            var map = vehicle.VehicleMap;
            var pivot = new Vector3(map.Size.x / 2f, 0f, map.Size.z / 2f);
            var drawPos = (original.YOffset() - pivot).RotatedBy(rot.AsAngle) + vehiclePos;
            drawPos += OffsetFor(vehicle, rot);
            return drawPos;
        }

        public bool TryGetVehicleMap(Map map, out VehiclePawnWithMap vehicle, VehicleMapFlag flag = VehicleMapFlag.StructureCells)
        {
            if (map == null)
            {
                vehicle = null;
                return false;
            }

            var isVehicleMap = map.IsVehicleMapOf(out var vehicle2);
            var vehicleCaravan = vehicle2?.GetVehicleCaravan();
            if (isVehicleMap && (vehicleCaravan is null || !VehicleMapFramework.settings.drawPlanet))
            {
                vehicle = vehicle2;
                return true;
            }

            var vehicles =
                    (isVehicleMap
                    ? vehicleCaravan.Vehicles.OfType<VehiclePawnWithMap>()
                    : VehiclePawnWithMapCache.AllVehiclesOn(map))
                .OrderBy(v => (v.cachedDrawPos - original).MagnitudeHorizontalSquared());

            vehicle = vehicles
                .FirstOrDefault(v =>
            {
                var rect = new Rect(0f, 0f, v.VehicleMap.Size.x, v.VehicleMap.Size.z);
                var vector = original.ToVehicleMapCoord(v);
                var intVec = vector.ToIntVec3();
                if (!rect.Contains(new Vector2(vector.x, vector.z)))
                {
                    return false;
                }
                if (!v.CachedStructureCells.Contains(intVec))
                {
                    return true;
                }
                if (flag.HasFlag(VehicleMapFlag.StructureCells) && !v.CachedExpandableCells.Contains(intVec) && !v.CachedOutOfBoundsCells.Contains(intVec))
                {
                    return true;
                }
                if (flag.HasFlag(VehicleMapFlag.ExpandableCells) && v.CachedExpandableCells.Contains(intVec))
                {
                    return true;
                }
                return flag.HasFlag(VehicleMapFlag.OutOfBoundsCells) && v.CachedOutOfBoundsCells.Contains(intVec);
            });
            return vehicle != null;
        }

        public Vector3 ToThingBaseMapCoord(Thing thing)
        {
            return thing.IsOnVehicleMapOf(out var vehicle) ? original.ToBaseMapCoord(vehicle) : original;
        }
    }

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
            //if (vehicle.Spawned)
            //{
            //    var vehicleRect = vehicle.VehicleRect(true);
            //    cellRect = cellRect.MovedBy(-vehicleRect.Min);
            //    return cellRect.ClipInsideMap(vehicle.VehicleMap);
            //}
            return cellRect = vehicle.VehicleMap.BoundsRect();
        }
        return cellRect.ClipInsideMap(map);
    }

    public static CellRect MovedOccupiedDrawRect(this Thing t)
    {
        var drawSize = t.DrawSize;
        return GenAdj.OccupiedRect(t.PositionOnBaseMap(), t.BaseRotation(), new IntVec2(Mathf.CeilToInt(drawSize.x), Mathf.CeilToInt(drawSize.y)));
    }

    public static Matrix4x4 ToBaseMapCoord(this Matrix4x4 matrix, VehiclePawnWithMap vehicle)
    {
        var rootPos = matrix.Position();
        matrix.SetColumn(3, rootPos.ToBaseMapCoord(vehicle).WithY(rootPos.y));
        return matrix;
    }

    extension(IntVec3 original)
    {
        public IntVec3 ToBaseMapCoord()
        {
            return original.ToVector3Shifted().ToBaseMapCoord().ToIntVec3();
        }

        public IntVec3 ToBaseMapCoord(VehiclePawnWithMap vehicle)
        {
            return original.ToVector3Shifted().ToBaseMapCoord(vehicle).ToIntVec3();
        }

        public IntVec3 ToBaseMapCoord(Map map)
        {
            return original.ToVector3Shifted().ToBaseMapCoord(map).ToIntVec3();
        }

        public Vector3 ToBaseMapCoord(VehiclePawnWithMap vehicle, Rot8 rot)
        {
            return original.ToVector3Shifted().ToBaseMapCoord(vehicle, rot);
        }

        public IntVec2 ToHitCell(VehiclePawnWithMap vehicle)
        {
            var orig = Vector3.zero.ToBaseMapCoord(vehicle).ToVehicleMapCoord(vehicle).ToIntVec3();
            return (orig + original).ToIntVec2;
        }
    }

    public static Vector3 OffsetFor(VehiclePawnWithMap vehicle)
    {
        return OffsetFor(vehicle, vehicle.FullRotation).RotatedBy(vehicle.Transform.rotation);
    }

    public static Vector3 OffsetFor(VehiclePawnWithMap vehicle, Rot8 rot)
    {
        var offset = Vector3.zero;
        var vehicleMap = vehicle.def.GetModExtension<VehicleMapProps>();
        if (vehicleMap == null) return offset;

        offset = rot.AsByte switch
        {
            Rot8.NorthInt => OffsetNorth(),
            Rot8.EastInt => vehicleMap.offsetEast ?? (vehicleMap.offsetWest == null
                ? vehicleMap.offsetEast = vehicleMap.offsetWest = vehicleMap.offset
                : vehicleMap.offsetEast = vehicleMap.offsetWest.Value.MirrorHorizontal()).Value,
            Rot8.SouthInt => OffsetSouth(),
            Rot8.WestInt => vehicleMap.offsetWest ?? (vehicleMap.offsetEast == null
                ? vehicleMap.offsetWest = vehicleMap.offsetEast = vehicleMap.offset
                : vehicleMap.offsetWest = vehicleMap.offsetEast.Value.MirrorHorizontal()).Value,
            Rot8.NorthEastInt => vehicleMap.offsetNorthEast ??=
                (vehicleMap.offsetNorthWest ??= OffsetNorth().RotatedBy(-45f)).MirrorHorizontal(),
            Rot8.SouthEastInt => vehicleMap.offsetSouthEast ??=
                (vehicleMap.offsetSouthWest ??= OffsetSouth().RotatedBy(45f)).MirrorHorizontal(),
            Rot8.SouthWestInt => vehicleMap.offsetSouthWest ??=
                (vehicleMap.offsetSouthEast ??= OffsetSouth().RotatedBy(-45f)).MirrorHorizontal(),
            Rot8.NorthWestInt => vehicleMap.offsetNorthWest ??=
                (vehicleMap.offsetNorthEast ??= OffsetNorth().RotatedBy(45f)).MirrorHorizontal(),
            _ => offset
        };
        return offset;

        Vector3 OffsetNorth() => vehicleMap.offsetNorth ?? (vehicleMap.offsetSouth == null ? vehicleMap.offsetNorth = vehicleMap.offsetSouth = vehicleMap.offset : vehicleMap.offsetNorth = vehicleMap.offsetSouth.Value.MirrorVertical()).Value;

        Vector3 OffsetSouth() => vehicleMap.offsetSouth ?? (vehicleMap.offsetNorth == null ? vehicleMap.offsetSouth = vehicleMap.offsetNorth = vehicleMap.offset : vehicleMap.offsetNorth = vehicleMap.offsetNorth.Value.MirrorVertical()).Value;
    }

    public static Rot4 RotationForPrint(this Thing thing)
    {
        var rot = thing.Rotation;

        if (RotForPrint != Rot4.North && (thing.def.size.x != thing.def.size.z || ((thing.def.rotatable || (thing.def.graphicData?.drawRotated ?? false)) && thing.Graphic is Graphic_Multi && !SameMaterialByRot())))
        {
            rot.AsInt += RotForPrint.AsInt;
        }
        return rot;

        bool SameMaterialByRot()
        {
            var graphic = thing.Graphic;
            var rotation = new Rot4(rot.AsInt + RotForPrint.AsInt);
            return graphic != null && graphic.MatAt(rot, thing) == graphic.MatAt(rotation, thing) && graphic.DrawOffset(rot) == graphic.DrawOffset(rotation);
        }
    }

    public static float PrintExtraRotation(Thing thing)
    {
        var result = 0f;
        if (thing.IsOnVehicleMapOf(out _))
        {
            result -= RotForPrint.AsAngle;
        }
        return result;
    }

    public static Map BaseMap(this Zone zone)
    {
        if (zone.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            return vehicle.Map;
        }
        return zone.Map;
    }

    public static Map BaseMap(this ref GlobalTargetInfo target)
    {
        return target.Map.BaseMap();
    }

    extension(Thing thing)
    {
        public IntVec3 PositionOnBaseMap()
        {
            if (!thing.IsOnVehicleMapOf(out var vehicle)) return thing.Position;
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(thing.Map);
            if (component.cachedPosOnBaseMap.TryGetValue(thing, out var pos))
            {
                return pos;
            }
            pos = thing.Position.ToBaseMapCoord(vehicle);
            component.cachedPosOnBaseMap[thing] = pos;
            return pos;
        }

        public IntVec3 PositionOnBaseMapSpawned()
        {
            if (!thing.IsOnVehicleMapOf(out var vehicle) || !vehicle.Spawned) return thing.Position;
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(thing.Map);
            if (component.cachedPosOnBaseMap.TryGetValue(thing, out var pos))
            {
                return pos;
            }
            pos = thing.Position.ToBaseMapCoord(vehicle);
            component.cachedPosOnBaseMap[thing] = pos;
            return pos;
        }
    }

    public static IntVec3 PositionOnBaseMap(this IHaulDestination dest)
    {
        return dest.Map.IsVehicleMapOf(out var vehicle) ? dest.Position.ToBaseMapCoord(vehicle) : dest.Position;
    }

    public static IntVec3 PositionHeldOnBaseMap(this Thing thing)
    {
        if (thing.Spawned)
        {
            return thing.PositionOnBaseMap();
        }
        var rootPosition = IntVec3.Invalid;
        var holder = thing.ParentHolder;
        while (holder != null)
        {
            rootPosition = holder switch
            {
                Thing thing2 when thing2.PositionOnBaseMap().IsValid => thing2.PositionOnBaseMap(),
                ThingComp thingComp when thingComp.parent.PositionOnBaseMap().IsValid => thingComp.parent
                    .PositionOnBaseMap(),
                _ => rootPosition
            };

            holder = holder.ParentHolder;
        }
        return rootPosition.IsValid ? rootPosition : thing.PositionOnBaseMap();
    }

    extension(IntVec3 origin)
    {
        public IntVec3 ToThingMapCoord(Thing thing)
        {
            return thing.IsOnVehicleMapOf(out var vehicle) ? origin.ToVehicleMapCoord(vehicle) : origin;
        }

        public IntVec3 ToThingBaseMapCoord(Thing thing)
        {
            return thing.IsOnVehicleMapOf(out var vehicle) ? origin.ToBaseMapCoord(vehicle) : origin;
        }
    }

    extension(ref LocalTargetInfo target)
    {
        public IntVec3 CellOnBaseMap()
        {
            return target.HasThing ? target.Thing.PositionOnBaseMap() : target.Cell;
        }

        public IntVec3 CellOnBaseMapSpawned()
        {
            return target.Thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned
                ? target.Cell.ToBaseMapCoord(vehicle) : target.Cell;
        }
    }

    public static IntVec3 CellOnBaseMap(this ref TargetInfo target)
    {
        return target.Map.IsVehicleMapOf(out var vehicle) ? target.Cell.ToBaseMapCoord(vehicle) : target.Cell;
    }

    public static IntVec3 CellOnBaseMap(this ref GlobalTargetInfo target)
    {
        return target.Map.IsVehicleMapOf(out var vehicle) ? target.Cell.ToBaseMapCoord(vehicle) : target.Cell;
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
        return target.Thing != null ?
            new TargetInfo(target.Thing) :
            new TargetInfo(target.CellOnBaseMap(), map);
    }

    public static IntVec3 PositionOnAnotherThingMap(this Thing thing, Thing another)
    {
        return another.IsOnVehicleMapOf(out var vehicle) ?
            thing.PositionOnBaseMap().ToVehicleMapCoord(vehicle) :
            thing.PositionOnBaseMap();
    }

    public static IntVec3 CellOnAnotherThingMap(this LocalTargetInfo target, Thing another)
    {
        if (target.HasThing)
        {
            return target.Thing.PositionOnAnotherThingMap(another);
        }
        return another.IsOnVehicleMapOf(out var vehicle) ? target.Cell.ToVehicleMapCoord(vehicle) : target.Cell;
    }

    public static IntVec3 CellOnAnotherMap(this IntVec3 cell, Map another)
    {
        return another.IsVehicleMapOf(out var vehicle) ? cell.ToVehicleMapCoord(vehicle) : cell;
    }

    extension(Thing thing)
    {
        public Rot4 BaseRotation()
        {
            return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) ?
                new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt) :
                thing.Rotation;
        }

        public Rot4 BaseRotationVehicleDraw()
        {
            return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) ?
                new Rot4(thing.Rotation.AsInt + vehicle.FullRotation.RotForVehicleDraw().AsInt) :
                thing.Rotation;
        }
        
        public Rot8 BaseFullRotation()
        {
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                return new Rot8(Rot8.FromIntClockwise((new Rot8(thing.Rotation).AsIntClockwise + vehicle.FullRotation.AsIntClockwise) % 8));
            }
            return thing.Rotation;
        }

        public Rot4 BaseFullRotationAsRot4()
        {
            return thing.BaseFullRotation().AsRot4Force();
        }

        public Rot8 BaseFullRotationDoor()
        {
            if (!thing.IsOnNonFocusedVehicleMapOf(out var vehicle)) return thing.Rotation;
            var rot = new Rot8(Rot8.FromIntClockwise((new Rot8(thing.Rotation).AsIntClockwise + vehicle.FullRotation.AsIntClockwise) % 8));
            return rot.FacingCell.z < 0 ? rot.Opposite : rot;
        }
    }

    extension(IntVec3 c)
    {
        public Rot4 DirectionToInsideMap(VehiclePawnWithMap vehicle)
        {
            return CellRect.WholeMap(vehicle.VehicleMap).GetClosestEdge(c).Opposite;
        }

        public Rot8 BaseFullDirectionToInsideMap(VehiclePawnWithMap vehicle)
        {
            var dir = c.DirectionToInsideMap(vehicle);
            var map = vehicle.VehicleMap;
            if (Find.CurrentMap != map || VehicleMapFramework.settings.drawPlanet)
            {
                return new Rot8(Rot8.FromIntClockwise((new Rot8(dir).AsIntClockwise + vehicle.FullRotation.AsIntClockwise) % 8));
            }
            return dir;
        }
    }

    public static int HalfLength(this VehicleDef vehicleDef)
    {
        return vehicleDef.size.z / 2;
    }

    public static Rot4 RotForVehicleDraw(this Rot8 rot)
    {
        if (rot.IsDiagonal)
        {
            return rot == Rot8.NorthEast || rot == Rot8.NorthWest ? Rot4.North : Rot4.South;
        }
        return rot;
    }

    public static IntVec2 BaseRotatedSize(Thing thing)
    {
        return !thing.BaseRotation().IsHorizontal
            ? thing.def.size
            : new IntVec2(thing.def.size.z, thing.def.size.x);
    }

    public static float VehicleMapMass(VehiclePawnWithMap vehicle)
    {
        var mass = CollectionsMassCalculator.MassUsage(vehicle.VehicleMap.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true);
        if (MultiFloors.Active)
        {
            var component = vehicle.VehicleMap.GetComponent(MultiFloors.MF_LevelMapComp);
            mass += ((IEnumerable<Map>)MultiFloors.GetOtherMapVerticallyOutwardFromCache(null, vehicle.VehicleMap, component, -1))
                .Sum(map => CollectionsMassCalculator.MassUsage(map.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true));
        }
        return mass;
    }

    public static Vector3 RotateForPrintNegate(Vector3 vector)
    {
        return vector.RotatedBy(-RotForPrint.AsAngle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetVehicleMap(this IntVec3 c, Map map, out VehiclePawnWithMap vehicle)
    {
        vehicle = MapComponentCache<VehicleMapGrid>.GetComponent(map).VehicleAt(c);
        return vehicle != null;
    }

    extension(Thing thing)
    {
        public void VirtualMapTransfer(Map map)
        {
            VirtualTeleporter.mapIndexOrState(thing) = (sbyte)map.Index;
        }

        public void VirtualMapTransfer(Map map, IntVec3 c)
        {
            VirtualTeleporter.mapIndexOrState(thing) = (sbyte)map.Index;
            thing.SetPositionDirect(c);
        }
    }

    //thingが車両マップ上にあったらthingの中心を基準として位置と回転を下の車両基準に回転するわよ
    public static void SetTRSOnVehicle(ref Matrix4x4 matrix, Vector3 pos, Quaternion q, Vector3 s, Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            var rot = vehicle.FullRotation;
            var angle = rot.AsAngle + vehicle.Transform.rotation;
            matrix = Matrix4x4.TRS(Ext_Math.RotatePoint(pos, thing.TrueCenter(), -angle), q * rot.AsQuat(), s);
            return;
        }
        matrix = Matrix4x4.TRS(pos, q, s);
    }

    public static Vector3 SelectedDrawPosOffset(Vector3 original, IntVec3 center)
    {
        VehiclePawnWithMap vehicle = null;
        return Find.Selector.SelectedObjects
            .Any(o => o is Thing thing && thing.Position == center && thing.IsOnNonFocusedVehicleMapOf(out vehicle))
            ? original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor())
            : original;
    }

    public static Vector3 FocusedDrawPosOffset(Vector3 original)
    {
        return FocusedOnVehicleMap(out var vehicle)
            ? original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor()) : original;
    }

    public static Vector3 FocusedOrSelectedDrawPosOffset(Vector3 original, IntVec3 center)
    {
        Thing thing;
        if ((thing = Find.Selector.SelectedObjects.OfType<Thing>().FirstOrDefault(t => t.Position == center)) != null)
        {
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
            }
        }
        else if (FocusedOnVehicleMap(out var vehicle))
        {
            return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
        }
        return original;
    }

    extension(VehiclePawn vehicle)
    {
        public float AngleRotated()
        {
            return Ext_Math.RotateAngle(vehicle.Angle, vehicle.Transform.rotation);
        }

        public float FullAngle()
        {
            return Ext_Math.RotateAngle(vehicle.FullRotation.AsAngle, vehicle.Transform.rotation);
        }

        public Quaternion FullAngleQuat()
        {
            return Quaternion.AngleAxis(vehicle.FullAngle(), Vector3.up);
        }

        public int HalfLength()
        {
            return vehicle.VehicleDef.HalfLength();
        }

        public bool TryGetFullRotation(ref Rot8 rot)
        {
            var map = vehicle.Map;
            if (map.IsNonFocusedVehicleMapOf(out _))
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

        public Rot8 BaseFullRotation()
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
    }

    public static IEnumerable<Thing> ColonyThingsWillingToBuyOnVehicle(this VehiclePawnWithMap vehicle, ITrader trader)
    {
        var map = vehicle.VehicleMap;
        var enumerable = map.listerThings.AllThings.Where(x => x.def.category == ThingCategory.Item && TradeUtility.PlayerSellableNow(x, trader) && !x.Position.Fogged(map) && (map.areaManager.Home[x.Position] || x.IsInAnyStorage()));
        foreach (var item in enumerable)
        {
            yield return item;
        }

        if (ModsConfig.BiotechActive)
        {
            var list = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.GeneBank);
            foreach (var item3 in list
                         .Select(item2 => item2.TryGetComp<CompGenepackContainer>())
                         .Where(compGenepackContainer => compGenepackContainer != null)
                         .Select(compGenepackContainer => compGenepackContainer.ContainedGenepacks)
                         .SelectMany(containedGenepacks => containedGenepacks))
            {
                yield return item3;
            }
        }

        var enumerable2 = map.listerBuildings.AllColonistBuildingsOfType<IHaulSource>();
        foreach (var item4 in enumerable2)
        {
            foreach (var item5 in item4.GetDirectlyHeldThings())
            {
                yield return item5;
            }
        }

        if (trader is Pawn pawn && pawn.GetLord() == null)
        {
            yield break;
        }

        if (vehicle.Spawned) yield break;

        foreach (var item6 in from x in TradeUtility.AllSellableColonyPawns(map)
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

    private static readonly List<Thing> tmpList = [];

    extension(IntVec3 c)
    {
        public Pawn GetFirstPawnAcrossMaps(Map map)
        {
            var thingList = c.GetThingListAcrossMaps(map);
            foreach (var t in thingList)
            {
                if (t is Pawn result)
                {
                    return result;
                }
            }

            return null;
        }

        public Thing GetCoverOnThingMap(Map map, Thing thing)
        {
            var thingMap = thing?.MapHeld;
            if (thingMap == null) return c.GetCover(map);
            var c2 = c.ToBaseMapCoord(thingMap);
            return c2.InBounds(thingMap) ? c2.GetCover(thingMap) : c.GetCover(map);
        }

        public bool RoofedAcrossMaps(Map map)
        {
            if (c.Roofed(map))
            {
                return true;
            }
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                return c.ToBaseMapCoord(vehicle).Roofed(vehicle.Map);
            }
            var vehicle2 = map.GetCachedMapComponent<VehicleMapGrid>().VehicleAt(c);
            return vehicle2 != null && c.ToVehicleMapCoord(vehicle2).Roofed(vehicle2.VehicleMap);
        }
    }
}

[Flags]
public enum VehicleMapFlag
{
    None = 0,
    StructureCells = 1 << 0,
    ExpandableCells = 1 << 1,
    OutOfBoundsCells = 1 << 2,
    All = 0b111
}