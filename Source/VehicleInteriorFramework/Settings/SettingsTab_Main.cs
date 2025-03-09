using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleInteriors.Settings
{
    internal class SettingsTab_Main : SettingsTabDrawer
    {
        public override void ResetSettings()
        {
            base.ResetSettings();
            var defaultSettings = VehicleMapSettings.DefaultSettings;
            settings.drawPlanet = defaultSettings.drawPlanet;
            settings.roofedPatch = defaultSettings.roofedPatch;
            settings.weightFactor = defaultSettings.weightFactor;
            settings.threadingPathCost = defaultSettings.threadingPathCost;
            settings.minAreaForThreading = defaultSettings.minAreaForThreading;
            settings.drawVehicleMapGrid = defaultSettings.drawVehicleMapGrid;
            settings.debugToolPatches = defaultSettings.debugToolPatches;
        }

        public override void Draw(Rect inRect)
        {
            base.Draw(inRect);
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("VMF_Settings.DrawPlanet".Translate(), ref settings.drawPlanet);
            listingStandard.CheckboxLabeled("VMF_Settings.RoofedPatch".Translate(), ref settings.roofedPatch);
            listingStandard.SliderLabeled("VMF_Settings.WeightFactor".Translate(), null, null, ref settings.weightFactor, 0f, 3f);
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("VMF_Settings.ThreadingPathCost".Translate(), ref settings.threadingPathCost);
            if (settings.threadingPathCost)
            {
                listingStandard.SliderLabeled("VMF_Settings.MinAreaForThreading".Translate(), null, null, ref settings.minAreaForThreading, 0, 2500, 1, "2500", "0");
            }
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("(Debug) Draw vehicle map grid", ref settings.drawVehicleMapGrid);
            listingStandard.CheckboxLabeled("(Debug) Enable debug tool patches", ref settings.debugToolPatches);
            listingStandard.End();
        }
    }
}
