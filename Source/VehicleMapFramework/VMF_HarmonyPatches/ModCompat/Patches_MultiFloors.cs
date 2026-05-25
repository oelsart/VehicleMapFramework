using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_MultiFloors
{
  static Patches_MultiFloors()
  {
    if (MultiFloors.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.MultiFloors);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.MultiFloors)]
[HarmonyPatch(typeof(Map), nameof(Map.ExposeData))]
[PatchLevel(Level.Mandatory)]
public static class Patch_Map_ExposeData
{
  private static readonly AccessTools.FieldRef<Map, List<Thing>> loadedFullThings = AccessTools.FieldRefAccess<Map, List<Thing>>("loadedFullThings");

  public static void Postfix(Map __instance, List<Thing> ___loadedFullThings)
  {
    if (Scribe.mode != LoadSaveMode.LoadingVars) return;
    var thingIDs = ___loadedFullThings.OfType<Pawn>().Select(t => t.ThingID).ToList();
    var duplicates = thingIDs.GroupBy(id => id).Where(id => id.Count() > 1)
      .Select(group => group.Key).ToList();
    foreach (var duplicate in duplicates)
    {
      var thing = ___loadedFullThings.FindLast(t => t.ThingID == duplicate);
      if (thing is not null)
      {
        VMF_Log.Warning($"Duplicated pawn found: {thing}");
        ___loadedFullThings.Remove(thing);
      }
    }

    foreach (var map in Find.Maps)
    {
      if (map == __instance) continue;
      thingIDs.Clear();
      thingIDs.AddRange(loadedFullThings(map).OfType<Pawn>().Select(t => t.ThingID));
      var thing = ___loadedFullThings.FindLast(t => thingIDs.Contains(t.ThingID));
      if (thing is not null)
      {
        VMF_Log.Warning($"Duplicated pawn found: {thing}");
        ___loadedFullThings.Remove(thing);
      }
    }
  }
}

//[HarmonyPatchCategory(PatchCategories.MultiFloors)]
//[HarmonyPatch("MultiFloors.Maps.LevelMapGenerator", "SetupMapGenerator")]
//[PatchLevel(Level.Safe)]
//public static class Patch_LevelMapGenerator_SetupMapGenerator
//{
//    public static void Postfix(Thing entrance, ref MapGeneratorDef __result)
//    {
//        if (entrance.IsOnVehicleMapOf(out _) && __result?.defName == "MF_Basement")
//        {
//            var MF_BasementWithoutCaves = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("MF_BasementWithoutCaves");
//            if (MF_BasementWithoutCaves != null)
//            {
//                __result = MF_BasementWithoutCaves;
//            }
//        }
//    }
//}
