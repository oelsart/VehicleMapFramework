using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class CompProperties_TogglableOverlays : CompProperties
    {
        public CompProperties_TogglableOverlays()
        {
            this.compClass = typeof(CompTogglableOverlays);
        }

        public List<ExtraOverlayData> extraOverlays = new List<ExtraOverlayData>();
    }
}
