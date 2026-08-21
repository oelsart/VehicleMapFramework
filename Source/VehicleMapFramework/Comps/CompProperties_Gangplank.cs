using JetBrains.Annotations;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_Gangplank : CompProperties_VehicleEnterSpot
{
  public int length;
  
  public CompProperties_Gangplank()
  {
    compClass = typeof(CompGangplank);
  }
}