using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework.Settings;

internal class SettingsTab_DynamicPatches : SettingsTabDrawer
{
  public override int Index => 1;

  public override string Label => "VMF_Settings.Tab.DynamicPatches".Translate();

  protected override void ResetSettings()
  {
    base.ResetSettings();
    settings.dynamicPatchEnabled = VehicleMapSettings.Default.dynamicPatchEnabled;
    settings.dynamicUnpatchEnabled = VehicleMapSettings.Default.dynamicUnpatchEnabled;
    settings.dynamicPatchLevel = VehicleMapSettings.Default.dynamicPatchLevel;
    settings.roofedPatch = VehicleMapSettings.Default.roofedPatch;
    settings.debugToolPatches = VehicleMapSettings.Default.debugToolPatches;
  }

  public override void Draw(Rect inRect)
  {
    base.Draw(inRect);
    var listingStandard = new Listing_Standard();
    listingStandard.Begin(inRect);
    listingStandard.CheckboxLabeled("VMF_Settings.EnableDynamicPatches".Translate(), ref settings.dynamicPatchEnabled, "VMF_Settings.EnableDynamicPatches.Tooltip".Translate());
    if (settings.dynamicPatchEnabled)
    {
      listingStandard.CheckboxLabeled("VMF_Settings.EnableDynamicUnpatches".Translate(), ref settings.dynamicUnpatchEnabled, "VMF_Settings.EnableDynamicUnpatches.Tooltip".Translate());
      var label = "VMF_Settings.DynamicPatchLevel".Translate();
      var widthPct = 0.5f;
      var rect = listingStandard.GetRect(Text.CalcHeight(label, listingStandard.ColumnWidth * widthPct));
      Widgets.Label(rect.LeftPart(widthPct), label);

      var level = settings.dynamicPatchLevel;
      var min = (float)Level.Sensitive;
      var max = (float)Level.Safe;
      var rightPart = rect.RightPart(widthPct);
      settings.dynamicPatchLevel = (Level)Widgets.HorizontalSlider(rightPart, (float)level, min, max, label: $"VMF_PatchLevel.{level}".Translate(), roundTo: 1f);
      TooltipHandler.TipRegion(rightPart, $"VMF_PatchLevel.{level}.Tooltip".Translate());
    }
    listingStandard.CheckboxLabeled("VMF_Settings.RoofedPatch".Translate(), ref settings.roofedPatch);
    listingStandard.CheckboxLabeled("(Debug) Enable debug tool patches", ref settings.debugToolPatches);
    listingStandard.End();
  }
}
