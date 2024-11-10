namespace VehicleInteriors
{
    public class CompProperties_EngineLightOverlays : CompProperties_TogglableOverlays
    {
        public CompProperties_EngineLightOverlays()
        {
            compClass = typeof(CompProperties_EngineLightOverlays);
        }

        public float engineOnOpacity;

        public float inFlightOpacity;

        public float ignitionDuration;
    }
}
