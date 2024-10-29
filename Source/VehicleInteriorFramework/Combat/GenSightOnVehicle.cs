using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors.Jobs
{
    public static class GenSightOnVehicle
    {
        public static bool LineOfSight(IntVec3 start, IntVec3 end, Map map, bool skipFirstCell = false, Func<IntVec3, bool> validator = null, int halfXOffset = 0, int halfZOffset = 0)
        {
            bool flag;
            if (start.x == end.x)
            {
                flag = (start.z < end.z);
            }
            else
            {
                flag = (start.x < end.x);
            }
            int num = Mathf.Abs(end.x - start.x);
            int num2 = Mathf.Abs(end.z - start.z);
            int num3 = start.x;
            int num4 = start.z;
            int i = 1 + num + num2;
            int num5 = (end.x > start.x) ? 1 : -1;
            int num6 = (end.z > start.z) ? 1 : -1;
            num *= 4;
            num2 *= 4;
            num += halfXOffset * 2;
            num2 += halfZOffset * 2;
            int num7 = num / 2 - num2 / 2;
            IntVec3 intVec = default(IntVec3);
            while (i > 1)
            {
                intVec.x = num3;
                intVec.z = num4;
                if (!skipFirstCell || !(intVec == start))
                {
                    if (!intVec.CanBeSeenOverOnVehicle(map))
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

        public static bool CanBeSeenOverOnVehicle(this IntVec3 c, Map map)
        {
            if (!c.InBounds(map)) return true;
            Building edifice = c.GetEdifice(map);
            return edifice == null || edifice.CanBeSeenOver();
        }

        public static bool LineOfSightThingToTarget(Thing thing, LocalTargetInfo target, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
        {
            if (target.HasThing)
            {
                return GenSightOnVehicle.LineOfSightThingToThing(thing, target.Thing, skipFirstCell, validator);
            }
            var map1 = thing.Map;
            var map2 = thing.BaseMap();
            if (map1 != map2)
            {
                return GenSightOnVehicle.LineOfSightToThing(target.CellOnAnotherThingMap(thing), thing, map1, skipFirstCell, validator) &&
                    GenSightOnVehicle.LineOfSight(target.Cell, thing.PositionOnBaseMap(), map2);
            }
            return GenSightOnVehicle.LineOfSight(target.Cell, thing.PositionOnBaseMap(), map2);
        }

        public static bool LineOfSightThingToThing(Thing start, Thing end, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
        {
            var map1 = start.Map;
            var map2 = end.Map;
            if (map1 != map2)
            {
                return GenSightOnVehicle.LineOfSightToThing(end.PositionOnAnotherThingMap(start), start, start.Map, skipFirstCell, validator) &&
                    GenSightOnVehicle.LineOfSightToThing(start.PositionOnAnotherThingMap(end), end, end.Map, skipFirstCell, validator);
            }
            return GenSightOnVehicle.LineOfSightToThing(end.PositionOnAnotherThingMap(start), start, start.Map, skipFirstCell, validator);
        }

        public static bool LineOfSightToThing(IntVec3 start, Thing t, Map map, bool skipFirstCell = false, Func<IntVec3, bool> validator = null)
        {
            if (t.def.size == IntVec2.One)
            {
                return GenSightOnVehicle.LineOfSight(start, t.Position, map, skipFirstCell, validator, 0, 0);
            }
            foreach (IntVec3 end in t.OccupiedRect())
            {
                if (GenSightOnVehicle.LineOfSight(start, end, map, skipFirstCell, validator, 0, 0))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool LineOfSight(IntVec3 start, IntVec3 end, Map map)
        {
            return GenSightOnVehicle.LineOfSight(start, end, map, CellRect.SingleCell(start), CellRect.SingleCell(end), null);
        }

        public static bool LineOfSight(IntVec3 start, IntVec3 end, Map map, CellRect startRect, CellRect endRect, Func<IntVec3, bool> validator = null)
        {
            bool flag;
            if (start.x == end.x)
            {
                flag = (start.z < end.z);
            }
            else
            {
                flag = (start.x < end.x);
            }
            int num = Mathf.Abs(end.x - start.x);
            int num2 = Mathf.Abs(end.z - start.z);
            int num3 = start.x;
            int num4 = start.z;
            int i = 1 + num + num2;
            int num5 = (end.x > start.x) ? 1 : -1;
            int num6 = (end.z > start.z) ? 1 : -1;
            int num7 = num - num2;
            num *= 2;
            num2 *= 2;
            IntVec3 intVec = default(IntVec3);
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
                    if (!intVec.CanBeSeenOverOnVehicle(map))
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
            if (GenSightOnVehicle.LineOfSight(start, end, map, skipFirstCell, validator, 0, 0))
            {
                return true;
            }
            int num = (start * 2).DistanceToSquared(end * 2);
            for (int i = 0; i < 4; i++)
            {
                if ((start * 2).DistanceToSquared(end * 2 + GenAdj.CardinalDirections[i]) <= num && GenSightOnVehicle.LineOfSight(start, end, map, skipFirstCell, validator, GenAdj.CardinalDirections[i].x, GenAdj.CardinalDirections[i].z))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
