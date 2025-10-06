using System.Diagnostics.CodeAnalysis;

namespace VehicleMapFramework;

public class CompProperties_EngineLightOverlay : CompProperties_OpacityOverlay
{
    public float engineOnOpacity;

    public float engineOffOpacity;

    public float inFlightOpacity;

    public float ignitionDuration;
    
    public CompProperties_EngineLightOverlay()
    {
        compClass = typeof(CompEngineLightOverlay);
    }
}
