using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors;

public class TargetMapManager(World world) : WorldComponent(world)
{
    public static Dictionary<Thing, Map> TargetMap
    {
        get
        {
            var component = Find.World?.GetComponent<TargetMapManager>();
            if (component == null) return null;

            component.targetMap ??= [];
            return component.targetMap;
        }
    }

    public static bool HasTargetMap(Thing thing, out Map map)
    {
        if (thing == null)
        {
            map = null;
            return false;
        }
        return TargetMap.TryGetValue(thing, out map) && map != null;
    }

    public static IntVec3 TargetCellOnBaseMap(ref LocalTargetInfo targ, Thing thing)
    {
        return targ.HasThing ? targ.CellOnBaseMap() : HasTargetMap(thing, out var map) ? targ.Cell.ToBaseMapCoord(map) : targ.Cell;
    }

    public static IntVec3 PositionOnTargetMap(Thing thing)
    {
        if (HasTargetMap(thing, out var map))
        {
            if (map == thing.Map)
            {
                return thing.Position;
            }
            var pos = thing.PositionOnBaseMap();
            if (map.IsNonFocusedVehicleMapOf(out var vehicle))
            {
                pos = pos.ToVehicleMapCoord(vehicle);
            }
            return pos;
        }
        return thing.Position;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref targetMap, "TargetMap", LookMode.Reference, LookMode.Reference);
    }

    private Dictionary<Thing, Map> targetMap;
}
