using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class CompProperties_TogglableOverlays : VehicleCompProperties
    {
        public CompProperties_TogglableOverlays()
        {
            this.compClass = typeof(CompTogglableOverlays);
        }

        public override void PostDefDatabase()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                foreach (ExtraOverlayData extraOverlay in this.extraOverlays)
                {
                    var vehicleDef = DefDatabase<VehicleDef>.AllDefs.FirstOrDefault(d => d.comps.Contains(this));
                    if (vehicleDef != null)
                    {
                        GraphicOverlay graphicOverlay = GraphicOverlay.Create(extraOverlay.graphicDataOverlay, vehicleDef);
                        graphicOverlay.data.graphicData.RecacheLayerOffsets();
                        this.overlays.Add(graphicOverlay);
                    }
                }
            });
        }

        public List<ExtraOverlayData> extraOverlays = new List<ExtraOverlayData>();

        [Unsaved(false)]
        public readonly List<GraphicOverlay> overlays = new List<GraphicOverlay>();
    }
}