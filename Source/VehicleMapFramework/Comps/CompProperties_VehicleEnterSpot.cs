using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class CompProperties_VehicleEnterSpot : CompProperties
{

  public bool allowPassingVehicle;

  public CompProperties_VehicleEnterSpot()
  {
    compClass = typeof(CompVehicleEnterSpot);
  }
}
