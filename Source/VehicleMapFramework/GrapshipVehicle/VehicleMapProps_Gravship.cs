using RimWorld;
using Verse;

namespace VehicleMapFramework
{
    public class VehicleMapProps_Gravship : VehicleMapProps, IExposable
    {
        public VehicleMapProps_Gravship() { }

        public string defName;

        public Building_GravEngine engine;

        public string DefName => defName ??= $"GravshipVehicle{engine.GetHashCode()}_";
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref size, "size");
            Scribe_Values.Look(ref offset, "offset");
            Scribe_Collections.Look(ref outOfBoundsCells, "outOfBoundsCells");
        }
    }
}
