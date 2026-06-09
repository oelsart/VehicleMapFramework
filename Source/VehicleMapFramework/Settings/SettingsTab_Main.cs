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
        settings.autoGetOffPlayer = VehicleMapSettings.Default.autoGetOffPlayer;
        settings.autoGetOffNonPlayer = VehicleMapSettings.Default.autoGetOffNonPlayer;
        settings.crossMapJobProtect = VehicleMapSettings.Default.crossMapJobProtect;
        settings.includeMapThings = VehicleMapSettings.Default.includeMapThings;
        settings.aStarTraverse = VehicleMapSettings.Default.aStarTraverse;
        settings.joyPatches = VehicleMapSettings.Default.joyPatches;
        settings.treatAsPlayerHome = VehicleMapSettings.Default.treatAsPlayerHome;
        settings.colonistBarMode = VehicleMapSettings.Default.colonistBarMode;
        settings.drawVehicleMapGrid = VehicleMapSettings.Default.drawVehicleMapGrid;
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
        listingStandard.CheckboxLabeled("VMF_Settings.TreatAsCaravanInventory".Translate(), ref settings.includeMapThings);
        listingStandard.CheckboxLabeled("(Experimental) Improved map traversal reachability checks.", ref settings.aStarTraverse);
        listingStandard.CheckboxLabeled("(Experimental) Cross map joy search.", ref settings.joyPatches);
        listingStandard.CheckboxLabeled("(Experimental) Treat vehicle map as player home.", ref settings.treatAsPlayerHome);
        
        var label = "VMF_Settings.ColonistBarMode".Translate();
        const float widthPct = 0.5f;
        var rect = listingStandard.GetRect(Text.CalcHeight(label, listingStandard.ColumnWidth * widthPct));
        Widgets.Label(rect.LeftPart(widthPct), label);

        var mode = settings.colonistBarMode;
        const float min = (float)VehicleMapSettings.ShowVehiclesOnColonistBar.DontShow;
        const float max = (float)VehicleMapSettings.ShowVehiclesOnColonistBar.Always;
        var rightPart = rect.RightPart(widthPct);
        settings.colonistBarMode = (VehicleMapSettings.ShowVehiclesOnColonistBar)Widgets.HorizontalSlider(rightPart, (float)mode, min, max, label: $"VMF_ColonistBarMode.{mode}".Translate(), roundTo: 1f);
        
        listingStandard.CheckboxLabeled("(Debug) Draw vehicle map grid.", ref settings.drawVehicleMapGrid);
        listingStandard.End();
    }
}
