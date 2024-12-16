using Verse;

namespace VehicleInteriors
{
    public class CompProperties_VehicleEnterSpot : CompProperties
    {
        public CompProperties_VehicleEnterSpot()
        {
            this.compClass = typeof(CompVehicleEnterSpot);
        }

        public bool allowPassingVehicle;
    }
}
