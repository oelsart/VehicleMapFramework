using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.Settings;

internal class SettingsTab_Main : SettingsTabDrawer
{
    public override int Index => 0;

    public override string Label => "VMF_Settings.Tab.Main".Translate();

    public override void ResetSettings()
    {
        base.ResetSettings();
        var defaultSettings = VehicleMapSettings.DefaultSettings;
        settings.drawPlanet = defaultSettings.drawPlanet;
        settings.weightFactor = defaultSettings.weightFactor;
        settings.drawVehicleMapGrid = defaultSettings.drawVehicleMapGrid;
    }

    public override void Draw(Rect inRect)
    {
        base.Draw(inRect);
        var listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        listingStandard.CheckboxLabeled("VMF_Settings.DrawPlanet".Translate(), ref settings.drawPlanet);
        listingStandard.SliderLabeled("VMF_Settings.WeightFactor".Translate(), null, null, ref settings.weightFactor, 0f, 3f);
        listingStandard.CheckboxLabeled("VMF_Settings.AutoGetOffPlayer".Translate(), ref settings.autoGetOffPlayer);
        listingStandard.CheckboxLabeled("VMF_Settings.AutoGetOffNonPlayer".Translate(), ref settings.autoGetOffNonPlayer);
        listingStandard.CheckboxLabeled("(Debug) Draw vehicle map grid", ref settings.drawVehicleMapGrid);
        listingStandard.End();
    }
}
