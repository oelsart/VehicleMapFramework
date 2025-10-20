using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_DoNotHitMe
{
    public const string Category = "VMF_Patches_DoNotHitMe";

    static Patches_DoNotHitMe()
    {
        if (ModCompat.DoNotHitMe)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_DoNotHitMe.Category)]
[HarmonyPatch("OGDHM.MapComponent_OgDHM", "IsIgnored")]
[PatchLevel(Level.Safe)]
public static class Patch_MapComponent_OgDHM_IsIgnored
{
    public static void Prefix(ref MapComponent __instance, Thing thing, Map ___map)
    {
        if (thing.Map != null && thing.Map != ___map)
        {
            __instance = thing.Map.GetComponent(__instance.GetType());
        }
    }
}