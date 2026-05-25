using Verse;

namespace VehicleMapFramework;

public class CompProperties_Zipline : CompProperties_VehicleEnterSpot
{

  public GraphicData standbyGraphic;

  public CompProperties_Zipline()
  {
    compClass = typeof(CompZipline);
  }
}
