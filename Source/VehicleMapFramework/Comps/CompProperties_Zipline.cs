using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_Zipline : CompProperties_VehicleEnterSpot
{
  public GraphicData standbyGraphic;

  public CompProperties_Zipline()
  {
    compClass = typeof(CompZipline);
  }
}
