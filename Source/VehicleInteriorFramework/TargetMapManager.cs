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

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref targetMap, "TargetMap", LookMode.Reference, LookMode.Reference);
    }

    private Dictionary<Thing, Map> targetMap;
}
