using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class CompEngineLightOverlays : CompTogglableOverlays
    {
        new public CompProperties_EngineLightOverlays Props => (CompProperties_EngineLightOverlays)this.props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            return new List<Gizmo>();
        }

        public override void CompTick()
        {
            if (base.Vehicle.ignition.Drafted && !this.ignitionComplete)
            {
                if (this.ignitionTick == null)
                {
                    this.ignitionTick = Find.TickManager.TicksGame;
                }
                else
                {
                    var opacity = Mathf.Min((Find.TickManager.TicksGame - this.ignitionTick.Value) / this.Props.ignitionDuration, this.Props.engineOnOpacity);
                    if (opacity == this.Props.engineOnOpacity)
                    {
                        this.ignitionComplete = true;
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
            if(!base.Vehicle.ignition.Drafted && ignitionTick != null)
            {
                this.ignitionTick = null;
                this.ignitionComplete = false;
                this.landingComplete = false;
            }

            if (!this.landingComplete)
            {
                foreach (var graphicOverlay in base.Overlays)
                {
                    if (graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                    {
                        graphic.Opacity = Mathf.Max(0f, graphic.Opacity - 0.004f);
                        if (graphic.Opacity == 0f)
                        {
                            this.landingComplete = true;
                        }
                    }
                }
            }

            if (base.Vehicle.CompVehicleLauncher != null && base.Vehicle.CompVehicleLauncher.inFlight)
            {
                if (this.ignitionTick == null)
                {
                    this.ignitionTick = Find.TickManager.TicksGame;
                }
                foreach (var graphicOverlay in base.Overlays)
                {
                    if (graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                    {
                        var launchProtocol = base.Vehicle.CompVehicleLauncher.launchProtocol;
                        var timeInAnimation = launchProtocol is VTOLTakeoff vtol ? vtol.TimeInAnimationVTOL : launchProtocol.TimeInAnimation;
                        var opacity = Mathf.Min(graphic.Opacity + (this.Props.inFlightOpacity - graphic.Opacity) * timeInAnimation * 0.1f, this.Props.inFlightOpacity);
                        graphic.Opacity = opacity;
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.ignitionTick, "ignitionTick");
            Scribe_Values.Look(ref this.ignitionComplete, "ignitionComplete");
            Scribe_Values.Look(ref this.landingComplete, "landingComplete");
        }

        private int? ignitionTick = 0;

        private bool ignitionComplete;

        private bool landingComplete = true;
    }
}
