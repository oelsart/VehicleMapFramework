using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleInteriors;

public class CompProperties_TogglableOverlays : VehicleCompProperties
{
    public CompProperties_TogglableOverlays()
    {
        compClass = typeof(CompTogglableOverlays);
    }

    public override void PostDefDatabase()
    {
        LongEventHandler.ExecuteWhenFinished(delegate
        {
            foreach (ExtraOverlayData extraOverlay in extraOverlays)
            {
                var vehicleDef = DefDatabase<VehicleDef>.AllDefs.FirstOrDefault(d => d.comps.Contains(this));
                if (vehicleDef != null)
                {
                    GraphicOverlay graphicOverlay = GraphicOverlay.Create(extraOverlay.graphicDataOverlay, vehicleDef);
                    graphicOverlay.data.graphicData.RecacheLayerOffsets();
                    overlays.Add(graphicOverlay);
                }
            }
        });
    }

    public List<ExtraOverlayData> extraOverlays = [];

    [Unsaved(false)]
    public readonly List<GraphicOverlay> overlays = [];
}