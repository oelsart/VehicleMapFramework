using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_WirelessTransmitter : CompProperties_Power
{
  public GraphicData lightGraphic;

  public float powerLossFactor;
  public float radius;

  public CompProperties_WirelessTransmitter()
  {
    compClass = typeof(CompWirelessTransmitter);
  }
}
