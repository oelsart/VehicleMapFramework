using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class VehicleMapParentsComponent : WorldComponent
    {
        public VehicleMapParentsComponent(World world) : base(world) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref this.vehicleMaps, "vehicleMaps", LookMode.Deep);
        }

        public List<MapParent_Vehicle> vehicleMaps = new List<MapParent_Vehicle>();
    }
}
