using HarmonyLib;
using RimWorld.Planet;
using Verse;
using static VehicleMapFramework.ModCompat.AsAboveSoBelow;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AsAboveSoBelow
{
  static Patches_AsAboveSoBelow()
  {
    if (AsAboveSoBelow.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.AsAboveSoBelow);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyAfter(HarmonyId)]
[HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
[PatchLevel(Level.Safe)]
public static class Patch_MapGenerator_GenerateMap
{
  public static void Prefix(ref IntVec3 mapSize, MapParent parent)
  {
    if (parent is MapParent_Vehicle { Faction.IsPlayer: true })
    {
      var pendingLayout = PendingLayout.Invoke(null);
      var count = UpperLevels() + 1;
      bandCount.SetValue(pendingLayout, count);
      bandHeight.SetValue(pendingLayout, mapSize.z);
      // surfaceBandは0 (default)
      pending.SetValue(null, pendingLayout);
      mapSize = new IntVec3(mapSize.x, mapSize.y, count * SlotFor(mapSize.z));
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.ABSkyBandGen", "Generate")]
[PatchLevel(Level.Safe)]
public static class Patch_ABSkyBandGen_Generate
{
  public static void Postfix(Map map, MapComponent bands, int band)
  {
    if (map.IsVehicleMapOf(out var vehicle) &&
        Banded(bands))
    {
      vehicle.SpawnStructures(RectOfBand(map, band).Min);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Patch_CameraDriver_ABClampToBand", "Postfix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_CameraDriver_ABClampToBand_Postfix
{
  public static bool Prefix() => !Find.CurrentMap.IsVehicleMap || !VehicleMapFramework.settings.drawPlanet;
}