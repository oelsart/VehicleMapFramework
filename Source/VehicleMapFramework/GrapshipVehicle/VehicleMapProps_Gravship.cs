using Verse;

namespace VehicleMapFramework
{
    public class VehicleMapProps_Gravship : VehicleMapProps, IExposable
    {
        public VehicleMapProps_Gravship() { }

        public string DefName => "GravshipVehicle" + size.GetHashCode();
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref size, "size");
        }
    }
}
