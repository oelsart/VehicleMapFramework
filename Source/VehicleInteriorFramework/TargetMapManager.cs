using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class TargetMapManager : WorldComponent
    {
        public static Dictionary<Thing, Map> TargetMap
        {
            get
            {
                var component = Find.World?.GetComponent<TargetMapManager>();
                if (component == null) return null;

                if (component.targetMap == null)
                {
                    component.targetMap = new Dictionary<Thing, Map>();
                }
                return component.targetMap;
            }
        }

        public TargetMapManager(World world) : base(world)
        {
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
}
