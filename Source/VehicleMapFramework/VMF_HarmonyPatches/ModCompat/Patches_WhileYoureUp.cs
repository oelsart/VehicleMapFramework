using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_WhileYoureUp
{
  static Patches_WhileYoureUp()
  {
    if (WhileYoureUp)
    {
      VMF_Harmony.PatchCategory(PatchCategories.WhileYoureUp);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.WhileYoureUp)]
[HarmonyPatch("WhileYoureUp.Mod", "TryFindBestBetterStoreCellFor_MidwayToTarget")]
[PatchLevel(Level.Safe)]
public static class Patch_WhileYoureUp_Mod_TryFindBestBetterStoreCellFor_MidwayToTarget
{
  public static void Prefix(Thing thing, ref Map map)
  {
    map = thing.MapHeld ?? map;
  }
}
