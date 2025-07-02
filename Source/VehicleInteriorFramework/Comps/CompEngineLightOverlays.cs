using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors;

public class CompEngineLightOverlays : CompTogglableOverlays
{
    new public CompProperties_EngineLightOverlays Props => (CompProperties_EngineLightOverlays)props;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        return new List<Gizmo>();
    }

    public override void CompTick()
    {
        if (base.Vehicle.ignition.Drafted && !ignitionComplete)
        {
            if (ignitionTick == null)
            {
                ignitionTick = Find.TickManager.TicksGame;
            }
            else
            {
                var opacity = Mathf.Min((Find.TickManager.TicksGame - ignitionTick.Value) / Props.ignitionDuration, Props.engineOnOpacity);
                if (opacity == Props.engineOnOpacity)
                {
                    ignitionComplete = true;
                }
                foreach (var graphicOverlay in base.Overlays)
                {
                    if (graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                    {
                        graphic.Opacity = opacity;
                    }
                }
            }
        }
        if (!base.Vehicle.ignition.Drafted && ignitionTick != null)
        {
            ignitionTick = null;
            ignitionComplete = false;
            landingComplete = false;
        }

        if (!landingComplete)
        {
            foreach (var graphicOverlay in base.Overlays)
            {
                if (graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                {
                    graphic.Opacity = Mathf.Max(0f, graphic.Opacity - 0.004f);
                    if (graphic.Opacity == 0f)
                    {
                        landingComplete = true;
                    }
                }
            }
        }

        if (base.Vehicle.CompVehicleLauncher != null && base.Vehicle.CompVehicleLauncher.inFlight)
        {
            ignitionTick ??= Find.TickManager.TicksGame;
            foreach (var graphicOverlay in base.Overlays)
            {
                if (graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                {
                    var launchProtocol = base.Vehicle.CompVehicleLauncher.launchProtocol;
                    var timeInAnimation = launchProtocol is VTOLTakeoff vtol ? vtol.TimeInAnimationVTOL : launchProtocol.TimeInAnimation;
                    var opacity = Mathf.Min(graphic.Opacity + ((Props.inFlightOpacity - graphic.Opacity) * timeInAnimation * 0.1f), Props.inFlightOpacity);
                    graphic.Opacity = opacity;
                }
            }
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref ignitionTick, "ignitionTick");
        Scribe_Values.Look(ref ignitionComplete, "ignitionComplete");
        Scribe_Values.Look(ref landingComplete, "landingComplete");
    }

    private int? ignitionTick = 0;

    private bool ignitionComplete;

    private bool landingComplete = true;
}
