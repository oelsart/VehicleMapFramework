using RimWorld.Planet;
using Verse;

namespace VehicleInteriors
{
    public class MapParent_Vehicle : MapParent
    {
        public VehiclePawnWithMap vehicle;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.vehicle, "vehicle");
        }
    }
}