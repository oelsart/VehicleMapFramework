using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class TargetMapManager : WorldComponent
    {
        public Dictionary<Thing, Map> TargetMap
        {
            get
            {
                if (targetMap == null)
                {
                    targetMap = new Dictionary<Thing, Map>();
                }
                return targetMap;
            }
        }

        public TargetMapManager(World world) : base(world)
        {
        }

        public static bool HasTargetMap(Thing thing, out Map map)
        {
            return Find.World.GetComponent<TargetMapManager>().TargetMap.TryGetValue(thing, out map) && map != null;
        }

        public static IntVec3 TargetCellOnBaseMap(ref LocalTargetInfo targ, Thing thing)
        {
            return targ.HasThing ? targ.CellOnBaseMap() : HasTargetMap(thing, out var map) ? targ.Cell.ToBaseMapCoord(map) : targ.Cell;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref targetMap, "TargetMap", LookMode.Reference, LookMode.Reference);
        }

        public Dictionary<Thing, Map> targetMap;
    }
}
