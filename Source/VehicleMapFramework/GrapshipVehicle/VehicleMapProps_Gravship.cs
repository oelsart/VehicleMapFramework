using Verse;

namespace VehicleMapFramework
{
    public class VehicleMapProps_Gravship : VehicleMapProps_Unique
    {
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref offset, "offset");
            Scribe_Values.Look(ref size, "size");
            Scribe_Collections.Look(ref outOfBoundsCells, "outOfBoundsCells");
        }
    }
}
