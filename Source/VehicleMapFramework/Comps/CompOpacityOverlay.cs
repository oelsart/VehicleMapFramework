using SmashTools.Performance;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompOpacityOverlay : VehicleComp
{
    public CompProperties_OpacityOverlay Props => (CompProperties_OpacityOverlay)props;

    public GraphicOverlay Overlay
    {
        get
        {
            overlay ??= Vehicle?.DrawTracker?.overlayRenderer?.AllOverlaysListForReading.FirstOrDefault(o => o.data?.identifier == Props.identifier);
            return overlay;
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        var overlay = Overlay;
        if (overlay == null) yield break;

        var tex = ContentFinder<Texture2D>.Get(overlay.Graphic.path + "_east");
        Vector2 proportion;
        if (tex.height > tex.width)
        {
            proportion = new Vector2(tex.height / (float)tex.height, 1f);
        }
        else
        {
            proportion = new Vector2(1f, tex.height / (float)tex.width);
        }

        yield return new Command_Action
        {
            defaultLabel = Props.label,
            icon = tex,
            iconProportions = proportion,
            action = () =>
            {
                var rect = new Rect(UI.MousePositionOnUIInverted - new Vector2(75f, 18f), new Vector2(150f, 33f));
                if (overlay.Graphic is Graphic_VehicleOpacity graphic)
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

    public override void PostExposeData()
    {
        base.PostExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            if (Overlay?.Graphic is Graphic_VehicleOpacity graphic)
            {
                tmpOpacity = graphic.Opacity;
                Scribe_Values.Look(ref tmpOpacity, Props.identifier + "Opacity", 1f);
            }
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Scribe_Values.Look(ref tmpOpacity, Props.identifier + "Opacity", 1f);
        }
        else if (Scribe.mode == LoadSaveMode.PostLoadInit)
        { 
            UnityThread.ExecuteOnMainThread(() =>
            {
                if (Overlay?.Graphic is Graphic_VehicleOpacity graphic)
                {
                    graphic.Opacity = tmpOpacity;
                }
            });
        }
    }

    private GraphicOverlay overlay;

    private float tmpOpacity;
}
