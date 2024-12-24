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
            base.ExposeData();
            Scribe_Collections.Look(ref VehicleMapParentsComponent.vehicleMaps, "vehicleMaps", LookMode.Deep);
        }

        public static List<MapParent_Vehicle> vehicleMaps = new List<MapParent_Vehicle>();
    }
}
