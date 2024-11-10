using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class VehicleMapParentsComponent : GameComponent
    {
        public VehicleMapParentsComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref VehicleMapParentsComponent.vehicleMaps, "vehicleMaps", LookMode.Deep);
        }

        public static List<MapParent_Vehicle> vehicleMaps = new List<MapParent_Vehicle>();
    }
}
