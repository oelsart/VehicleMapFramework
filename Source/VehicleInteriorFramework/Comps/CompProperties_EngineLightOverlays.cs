﻿namespace VehicleInteriors
{
    public class CompProperties_EngineLightOverlays : CompProperties_TogglableOverlays
    {
        public CompProperties_EngineLightOverlays()
        {
            this.compClass = typeof(CompEngineLightOverlays);
        }

        public float engineOnOpacity;

        public float inFlightOpacity;

        public float ignitionDuration;
    }
}