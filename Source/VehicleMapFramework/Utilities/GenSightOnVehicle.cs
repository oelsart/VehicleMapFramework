using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public static class GenSightOnVehicle
{
    public static bool LineOfSight(IntVec3 start, IntVec3 end, Map map, bool skipFirstCell, Func<IntVec3, bool> validator = null, int halfXOffset = 0, int halfZOffset = 0)
    {
        bool flag;
        if (map.IsVehicleMapOf(out var vehicle))
        {
            if (vehicle.Spawned)
            {
                start = start.ToBaseMapCoord(vehicle);
                end = end.ToBaseMapCoord(vehicle);
                map = vehicle.Map;
            }
            else return LineOfSightVehicleToVehicle(start, end, map, skipFirstCell, validator, halfXOffset, halfZOffset);
        }
        if (!start.InBounds(map) || !end.InBounds(map)) return false;

        if (start.x == end.x)
        {
            flag = start.z < end.z;
        }
        else
        {
            flag = start.x < end.x;
        }
        var num = Mathf.Abs(end.x - start.x);
        var num2 = Mathf.Abs(end.z - start.z);
        var num3 = start.x;
        var num4 = start.z;
        var i = 1 + num + num2;
        var num5 = (end.x > start.x) ? 1 : -1;
        var num6 = (end.z > start.z) ? 1 : -1;
        num *= 4;
        num2 *= 4;
        num += halfXOffset * 2;
        num2 += halfZOffset * 2;
        var num7 = (num / 2) - (num2 / 2);
        IntVec3 intVec = default;
        while (i > 1)
        {
            intVec.x = num3;
            intVec.z = num4;
            if (!skipFirstCell || !(intVec == start))
            {
                if (!intVec.CanBeSeenOverOnVehicleFast(map))
                {
                    return false;
                }
                if (validator != null && !validator(intVec))
                {
                    return false;
                }
            }
            if (num7 > 0 || (num7 == 0 && flag))
            {
                num3 += num5;
                num7 -= num2;
            }
            else
            {
                num4 += num6;
                num7 += num;
            }
            i--;
        }
        return true;
    }

    public static bool LineOfSightVehicleToVehicle(IntVec3 start, IntVec3 end, Map map, bool skipFirstCell = false,
        Func<IntVec3, bool> validator = null, int halfXOffset = 0, int halfZOffset = 0)
    {
        return (!start.ToVector3Shifted().TryGetVehicleMap(map, out var vehicle2) || LOS(vehicle2)) &&
               (!end.ToVector3Shifted().TryGetVehicleMap(map, out var vehicle3) || LOS(vehicle3));
        
        bool LOS(VehiclePawnWithMap v)
        {
            var _start = start.ToVehicleMapCoord(v);
            var _end = end.ToVehicleMapCoord(v);
            var flag = _start.x == _end.x ? _start.z < _end.z : _start.x < _end.x;
            var num = Mathf.Abs(_end.x - _start.x);
            var num2 = Mathf.Abs(_end.z - _start.z);
            var num3 = _start.x;
            var num4 = _start.z;
            var i = 1 + num + num2;
            var num5 = (_end.x > _start.x) ? 1 : -1;
            var num6 = (_end.z > _start.z) ? 1 : -1;
            num *= 4;
            num2 *= 4;
            num += halfXOffset * 2;
            num2 += halfZOffset * 2;
            var num7 = (num / 2) - (num2 / 2);
            IntVec3 intVec = default;
            while (i > 1)
            {
                intVec.x = num3;
                intVec.z = num4;
                if (intVec.InBounds(v.VehicleMap) && (!skipFirstCell || !(intVec == _start)))
                {
                    if (!intVec.CanBeSeenOverFast(v.VehicleMap))
                    {
                        return false;
                    }

                    if (validator != null && !validator(intVec))
                    {
                        return false;
                    }
                }
                if (num7 > 0 || (num7 == 0 && flag))
                {
                    num3 += num5;
                    num7 -= num2;
                }
                else
                {
                    num4 += num6;
                    num7 += num;
                }
                i--;
            }
            return true;
        }
    }

    public static bool LineOfSightThingToTarget(Thing thing, LocalTargetInfo target, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
    {
        return LineOfSight(thing.PositionOnBaseMap(), target.CellOnBaseMap(), thing.BaseMap(), skipFirstCell, validator);
    }

    public static bool LineOfSightThingToThing(Thing start, Thing end, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
    {
        return LineOfSight(start.PositionOnBaseMap(), end.PositionOnBaseMap(), start.BaseMap(), skipFirstCell, validator);
    }

    public static bool LineOfSightToThing(IntVec3 start, Thing t, Map map, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
    {
        var flag = false;
        if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            start = start.ToBaseMapCoord(vehicle);
            map = vehicle.Map;
            flag = true;
        }
        return t.def.size == IntVec2.One
            ? LineOfSight(start, t.PositionOnBaseMap(), map, skipFirstCell, validator)
            : t.OccupiedRect().Select(end => flag ? end.ToBaseMapCoord(vehicle) : end)
                .Any(end2 => LineOfSight(start, end2, map, skipFirstCell, validator));
    }

    public static bool LineOfSight(IntVec3 start, IntVec3 end, Map map)
    {
        return LineOfSight(start, end, map, CellRect.SingleCell(start), CellRect.SingleCell(end));
    }

    public static bool LineOfSight(IntVec3 start, IntVec3 end, Map map, CellRect startRect, CellRect endRect, Func<IntVec3, bool> validator = null)
    {
        if (map.IsVehicleMapOf(out var vehicle))
        {
            if (vehicle.Spawned)
            {
                start = start.ToBaseMapCoord(vehicle);
                end = end.ToBaseMapCoord(vehicle);
                map = vehicle.Map;
            }
            else return LineOfSightVehicleToVehicle(start, end, map, false, validator);
        }
        if (!start.InBounds(map) || !end.InBounds(map)) return false;

        bool flag;
        if (start.x == end.x)
        {
            flag = start.z < end.z;
        }
        else
        {
            flag = start.x < end.x;
        }
        var num = Mathf.Abs(end.x - start.x);
        var num2 = Mathf.Abs(end.z - start.z);
        var num3 = start.x;
        var num4 = start.z;
        var i = 1 + num + num2;
        var num5 = (end.x > start.x) ? 1 : -1;
        var num6 = (end.z > start.z) ? 1 : -1;
        var num7 = num - num2;
        num *= 2;
        num2 *= 2;
        IntVec3 intVec = default;
        while (i > 1)
        {
            intVec.x = num3;
            intVec.z = num4;
            if (endRect.Contains(intVec))
            {
                return true;
            }
            if (!startRect.Contains(intVec))
            {
                if (!intVec.CanBeSeenOverOnVehicleFast(map))
                {
                    return false;
                }
                if (validator != null && !validator(intVec))
                {
                    return false;
                }
            }
            if (num7 > 0 || (num7 == 0 && flag))
            {
                num3 += num5;
                num7 -= num2;
            }
            else
            {
                num4 += num6;
                num7 += num;
            }
            i--;
        }
        return true;
    }

    public static bool LineOfSightToEdges(IntVec3 start, IntVec3 end, Map map, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
    {
        if (LineOfSight(start, end, map, skipFirstCell, validator))
        {
            return true;
        }
        var num = (start * 2).DistanceToSquared(end * 2);
        for (var i = 0; i < 4; i++)
        {
            if ((start * 2).DistanceToSquared((end * 2) + GenAdj.CardinalDirections[i]) <= num && LineOfSight(start, end, map, skipFirstCell, validator, GenAdj.CardinalDirections[i].x, GenAdj.CardinalDirections[i].z))
            {
                return true;
            }
        }
        return false;
    }

    extension(IntVec3 c)
    {
        public bool CanBeSeenOverOnVehicle(Map map)
        {
            if (!c.InBounds(map)) return false;

            var flag = true;
            if (c.TryGetVehicleMap(map, out var vehicle))
            {
                var c2 = c.ToVehicleMapCoord(vehicle);
                flag = !c2.InBounds(vehicle.VehicleMap);
                if (!flag)
                {
                    var edifice = c2.GetEdifice(vehicle.VehicleMap);
                    flag = edifice == null || edifice.CanBeSeenOver();
                }
            }
            var edifice2 = c.GetEdifice(map);
            return flag && (edifice2 == null || edifice2.CanBeSeenOver());
        }

        public bool CanBeSeenOverOnVehicleFast(Map map)
        {
            var flag = true;
            if (c.TryGetVehicleMap(map, out var vehicle))
            {
                var c2 = c.ToVehicleMapCoord(vehicle);
                flag = !c2.InBounds(vehicle.VehicleMap);
                if (!flag)
                {
                    var edifice = c2.GetEdifice(vehicle.VehicleMap);
                    flag = edifice == null || edifice.CanBeSeenOver();
                }
            }
            var edifice2 = c.GetEdifice(map);
            return flag && (edifice2 == null || edifice2.CanBeSeenOver());
        }
    }
}
