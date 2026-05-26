namespace VehicleMapFramework;

public class CompProperties_EngineLightOverlay : CompProperties_OpacityOverlay
{

  public float engineOffOpacity;
  public float engineOnOpacity;

  public float ignitionDuration;

  public float inFlightOpacity;

  public CompProperties_EngineLightOverlay()
  {
    compClass = typeof(CompEngineLightOverlay);
  }
}
