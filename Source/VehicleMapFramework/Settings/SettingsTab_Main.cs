using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.Settings;

internal class SettingsTab_Main : SettingsTabDrawer
{
    public override int Index => 0;

    public override string Label => "VMF_Settings.Tab.Main".Translate();

    protected override void ResetSettings()
    {
        base.ResetSettings();
        settings.drawPlanet = VehicleMapSettings.Default.drawPlanet;
        settings.weightFactor = VehicleMapSettings.Default.weightFactor;
        settings.drawVehicleMapGrid = VehicleMapSettings.Default.drawVehicleMapGrid;
        settings.includeMapThings = VehicleMapSettings.Default.includeMapThings;
        settings.crossMapJobProtect = VehicleMapSettings.Default.crossMapJobProtect;
        settings.aStarTraverse = VehicleMapSettings.Default.aStarTraverse;
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
        listingStandard.CheckboxLabeled("VMF_Settings.CrossMapJobProtect".Translate(), ref settings.crossMapJobProtect, "VMF_Settings.CrossMapJobProtect.Tooltip".Translate());
        listingStandard.CheckboxLabeled("(Experimental) Improved map traversal reachability checks.", ref settings.aStarTraverse);
        listingStandard.CheckboxLabeled("(Experimental) Count items on the vehicle map as part of the caravan's inventory.", ref settings.includeMapThings);
        listingStandard.CheckboxLabeled("(Debug) Draw vehicle map grid.", ref settings.drawVehicleMapGrid);
        listingStandard.End();
    }
}
