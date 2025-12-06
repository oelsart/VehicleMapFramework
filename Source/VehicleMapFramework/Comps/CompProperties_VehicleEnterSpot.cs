using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class CompProperties_VehicleEnterSpot : CompProperties
{
    public CompProperties_VehicleEnterSpot()
    {
        compClass = typeof(CompVehicleEnterSpot);
    }

    public bool allowPassingVehicle;
}
