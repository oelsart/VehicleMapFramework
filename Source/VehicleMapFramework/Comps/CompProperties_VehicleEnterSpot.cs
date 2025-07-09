using Verse;

namespace VehicleMapFramework;

public class CompProperties_VehicleEnterSpot : CompProperties
{
    public CompProperties_VehicleEnterSpot()
    {
        compClass = typeof(CompVehicleEnterSpot);
    }

    public bool allowPassingVehicle;
}
