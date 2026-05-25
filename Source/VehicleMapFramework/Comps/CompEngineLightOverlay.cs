using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompEngineLightOverlay : CompOpacityOverlay
{

  private bool ignitionComplete;

  private bool landingComplete;

  public new CompProperties_EngineLightOverlay Props => (CompProperties_EngineLightOverlay)props;

  public override IEnumerable<Gizmo> CompGetGizmosExtra()
  {
    yield break;
  }

  public override void CompTick()
  {
    if (Overlay?.Graphic is not Graphic_VehicleOpacity graphic) return;

    if (Vehicle.CompVehicleLauncher is { inFlight: true })
    {
      var launchProtocol = Vehicle.CompVehicleLauncher.launchProtocol;
      var timeInAnimation = launchProtocol is VTOLTakeoff vtol ? vtol.TimeInAnimationVTOL : launchProtocol.TimeInAnimation;
      var opacity = Mathf.Min(graphic.Opacity + (Props.inFlightOpacity - graphic.Opacity) * timeInAnimation * 0.1f, Props.inFlightOpacity);
      graphic.Opacity = opacity;
      return;
    }

    if (Vehicle.ignition.Drafted)
    {
      landingComplete = false;
      if (!ignitionComplete)
      {
        var offset = Props.engineOnOpacity - Props.engineOffOpacity;
        var num = offset / Props.ignitionDuration;
        graphic.Opacity += num;
        if (Mathf.Abs(Props.engineOnOpacity - graphic.Opacity) <= Mathf.Abs(num))
        {
          ignitionComplete = true;
          graphic.Opacity = Props.engineOnOpacity;
        }
      }
    }
    else
    {
      ignitionComplete = false;
      if (!landingComplete)
      {
        var offset = Props.engineOffOpacity - Props.engineOnOpacity;
        var num = offset / Props.ignitionDuration;
        graphic.Opacity += num;
        if (Mathf.Abs(Props.engineOffOpacity - graphic.Opacity) <= Mathf.Abs(num))
        {
          landingComplete = true;
          graphic.Opacity = Props.engineOffOpacity;
        }
      }
    }
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_Values.Look(ref ignitionComplete, "ignitionComplete");
    Scribe_Values.Look(ref landingComplete, "landingComplete");
  }
}
