namespace VehicleInteriors;

public class CompProperties_EngineLightOverlay : CompProperties_OpacityOverlay
{
    public CompProperties_EngineLightOverlay()
    {
        compClass = typeof(CompEngineLightOverlay);
    }

    public float engineOnOpacity;

    public float inFlightOpacity;

    public float ignitionDuration;
}
