using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors;

public class CompEngineLightOverlay : CompOpacityOverlay
{
    new public CompProperties_EngineLightOverlay Props => (CompProperties_EngineLightOverlay)props;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield break;
    }

    public override void CompTick()
    {
        var overlay = Overlay;
        var graphic = overlay?.Graphic as Graphic_VehicleOpacity;
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
                graphic?.Opacity = opacity;
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
            if (graphic != null)
            {
                graphic.Opacity = Mathf.Max(0f, graphic.Opacity - 0.004f);
                if (graphic.Opacity == 0f)
                {
                    landingComplete = true;
                }
            }
        }

        if (base.Vehicle.CompVehicleLauncher != null && base.Vehicle.CompVehicleLauncher.inFlight)
        {
            ignitionTick ??= Find.TickManager.TicksGame;
            if (graphic != null)
            {
                var launchProtocol = base.Vehicle.CompVehicleLauncher.launchProtocol;
                var timeInAnimation = launchProtocol is VTOLTakeoff vtol ? vtol.TimeInAnimationVTOL : launchProtocol.TimeInAnimation;
                var opacity = Mathf.Min(graphic.Opacity + ((Props.inFlightOpacity - graphic.Opacity) * timeInAnimation * 0.1f), Props.inFlightOpacity);
                graphic.Opacity = opacity;
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
