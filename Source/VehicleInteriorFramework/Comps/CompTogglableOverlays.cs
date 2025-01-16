using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class CompTogglableOverlays : VehicleComp
    {
        public CompProperties_TogglableOverlays Props => (CompProperties_TogglableOverlays)this.props;

        public IEnumerable<GraphicOverlay> Overlays => this.graphicOverlays.Values.Select(v => v.graphicOverlay);

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var graphicOverlay in this.graphicOverlays.Values)
            {
                var tex = ContentFinder<Texture2D>.Get(graphicOverlay.graphicOverlay.Graphic.path + "_east");
                Vector2 proportion;
                if (tex.height > tex.width)
                {
                    proportion = new Vector2((float)tex.height / (float)tex.height, 1f);
                }
                else
                {
                    proportion = new Vector2(1f, (float)tex.height / (float)tex.width);
                }

                yield return new Command_Action
                {
                    defaultLabel = graphicOverlay.label,
                    icon = tex,
                    iconProportions = proportion,
                    action = () =>
                    {
                        var rect = new Rect(UI.MousePositionOnUIInverted - new Vector2(75f, 18f), new Vector2(150f, 33f));
                        if (graphicOverlay.graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                        {
                            Find.WindowStack.Add(new EphemenalWindow()
                            {
                                windowRect = rect,
                                doWindowFunc = () =>
                                {
                                    Widgets.DrawWindowBackground(rect.AtZero(), GUI.color);
                                    graphic.Opacity = VMF_Widgets.HorizontalSlider(new Rect(0f, 15f, rect.width, rect.height), graphic.Opacity, 0f, 1f, false, null, "0%", "100%", -1, GUI.color);
                                }
                            });
                        }
                    }
                };
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            void Init()
            {
                var parent = this.Vehicle;
                foreach (var extraOverlay in this.Props.extraOverlays)
                {
                    if (!this.graphicOverlays.ContainsKey(extraOverlay.key))
                    {
                        var graphicOverlay = GraphicOverlay.Create(extraOverlay.graphicDataOverlay, parent);
                        this.graphicOverlays[extraOverlay.key] = (graphicOverlay, extraOverlay.label);
                        parent.graphicOverlay.AddOverlay(extraOverlay.key, graphicOverlay);

                        if (graphicOverlay.Graphic is Graphic_VehicleOpacity graphicOpacity && this.tmpOpacities.ContainsKey(extraOverlay.key))
                        {
                            graphicOpacity.Opacity = this.tmpOpacities[extraOverlay.key];
                        }
                    }
                }
            }

            if (!UnityData.IsInMainThread)
            {
                LongEventHandler.ExecuteWhenFinished(delegate
                {
                    Init();
                });
            }
            else
            {
                Init();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            var parent = this.Vehicle;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (var graphicOverlay in this.graphicOverlays)
                {
                    if (graphicOverlay.Value.graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                    {
                        var opacity = graphic.Opacity;
                        Scribe_Values.Look(ref opacity, graphicOverlay.Key + "Opacity");
                    }
                }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                foreach (var extraOverlay in this.Props.extraOverlays)
                {
                    if (extraOverlay.graphicDataOverlay.graphicData.Graphic is Graphic_VehicleOpacity)
                    {
                        var opacity = 1f;
                        Scribe_Values.Look(ref opacity, extraOverlay.key + "Opacity");
                        this.tmpOpacities[extraOverlay.key] = opacity;
                    }
                }
            }
        }

        private readonly Dictionary<string, float> tmpOpacities = new Dictionary<string, float>();

        private readonly Dictionary<string, (GraphicOverlay graphicOverlay, string label)> graphicOverlays = new Dictionary<string, (GraphicOverlay graphicOverlay, string label)>();
    }
}
