using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_ResourceDeliveryHelper
{
  static Patches_ResourceDeliveryHelper()
  {
    if (ResourceDeliveryHelper.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.ResourceDeliveryHelper);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ResourceDeliveryHelper)]
[HarmonyPatch("ResourceDeliveryHelper.ThingOverlays_ThingOverlaysOnGUI_Patch", "Postfix")]
[PatchLevel(Level.Safe)]
public static class Patches_ResourceDeliveryHelper_Postfix
{
  private static readonly List<Map> tmpMaps = [];
  
  public static void Postfix()
  {
    var currentMap = Find.CurrentMap;
    currentMap.VehicleMapsOnMap(tmpMaps);
    var currentViewRect = Find.CameraDriver.CurrentViewRect;
    var min = currentViewRect.Min;
    var max = currentViewRect.Max;
    var rememberedCameraPos = currentMap.rememberedCameraPos;
    var num = HashCode.Combine(rememberedCameraPos.rootPos.GetHashCode(), rememberedCameraPos.rootSize.GetHashCode());
    foreach (var map in tmpMaps)
    {
      if (!map.IsVehicleMapOf(out var vehicle))
        continue;

      using var scope = new Command_FocusVehicleMap.FocusVehicle(vehicle);
      var localViewRect = CellRect.FromLimits(min.ToVehicleMapCoord(vehicle), max.ToVehicleMapCoord(vehicle));
      foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
      {
        if (thing is Blueprint_Install) continue;
        ResourceDeliveryHelper.TryDisplayOverlay(thing, localViewRect, map, num);
      }

      foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
      {
        ResourceDeliveryHelper.TryDisplayOverlay(thing, localViewRect, map, num);
      }
    }
    
    tmpMaps.Clear();
  }
}