using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_WirelessTransmitter : CompProperties_Power
{
    public CompProperties_WirelessTransmitter()
    {
        compClass = typeof(CompWirelessTransmitter);
    }
    
    public float powerLossFactor;
    public float radius;
    public GraphicData lightGraphic;
}
