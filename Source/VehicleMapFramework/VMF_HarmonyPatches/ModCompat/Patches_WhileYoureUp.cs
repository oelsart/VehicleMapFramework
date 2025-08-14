using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_WhileYoureUp
{
    public const string Category = "VMF_Patches_WhileYoureUp";

    static Patches_WhileYoureUp()
    {
        if (ModCompat.WhileYoureUp)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_WhileYoureUp.Category)]
[HarmonyPatch("WhileYoureUp.Mod", "TryFindBestBetterStoreCellFor_MidwayToTarget")]
[PatchLevel(Level.Safe)]
public static class Patch_WhileYoureUp_Mod_TryFindBestBetterStoreCellFor_MidwayToTarget
{
    public static void Prefix(Thing thing, ref Map map)
    {
        map = thing.MapHeld ?? map;
    }
}