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

        public VehiclePawn ParentVehicle => (VehiclePawn)this.parent;

        public IEnumerable<GraphicOverlay> Overlays => this.graphicOverlays.Values.Select(v => v.graphicOverlay);

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var parent = this.ParentVehicle;
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
                                    graphic.Opacity = VIF_Widgets.HorizontalSlider(new Rect(0f, 15f, rect.width, rect.height), graphic.Opacity, 0f, 1f, false, null, "0%", "100%", -1, GUI.color);
                                }
                            });
                        }
                    }
                };
            }
        }

        public override void PostLoad()
        {
            void Init()
            {
                var parent = this.ParentVehicle;
                foreach (var extraOverlay in this.Props.extraOverlays)
                {
                    if (!this.graphicOverlays.ContainsKey(extraOverlay.key))
                    {
                        var graphicOverlay = GraphicOverlay.Create(extraOverlay.graphicDataOverlay, parent);
                        this.graphicOverlays[extraOverlay.key] = (graphicOverlay, extraOverlay.label);
                        parent.graphicOverlay.AddOverlay(extraOverlay.key, graphicOverlay);
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
            void Init()
            {
                foreach (var graphicOverlay in this.graphicOverlays)
                {
                    if (graphicOverlay.Value.graphicOverlay.Graphic is Graphic_VehicleOpacity graphic)
                    {
                        var opacity = graphic.Opacity;
                        Scribe_Values.Look(ref opacity, graphicOverlay.Key + "Opacity");
                        graphic.Opacity = opacity;
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

        private readonly Dictionary<string, (GraphicOverlay graphicOverlay, string label)> graphicOverlays = new Dictionary<string, (GraphicOverlay graphicOverlay, string label)>();
    }
}
