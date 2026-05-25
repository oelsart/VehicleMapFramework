using Verse;

namespace VehicleMapFramework;

public class CompProperties_PipeConnector : CompProperties
{

  public float radius;

  public CompProperties_PipeConnector()
  {
    compClass = typeof(CompPipeConnector);
  }
}
