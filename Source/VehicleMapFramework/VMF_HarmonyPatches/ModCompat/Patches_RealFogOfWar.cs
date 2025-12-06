using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_RealFogOfWar
{
    static Patches_RealFogOfWar()
    {
        if (RealFogOfWar)
        {
            VMF_Harmony.PatchCategory(PatchCategories.RealFogOfWar);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.RealFogOfWar)]
[HarmonyPatch("RimWorldRealFoW.CompViewBlockerWatcher", "updateViewBlockerCells")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompViewBlockerWatcher_updateViewBlockerCells
{
    public static bool Prefix(Map ___map) => !___map.IsVehicleMap;
}

[HarmonyPatchCategory(PatchCategories.RealFogOfWar)]
[HarmonyPatch("RimWorldRealFoW.Detours.DesignatorPlace", "CanDesignateCell_Postfix")]
public static class Patch_DesignatorPlace_CanDesignateCell_Postfix
{
    public static bool Prefix() => Command_FocusVehicleMap.FocusedVehicle is null;
}

[HarmonyPatchCategory(PatchCategories.RealFogOfWar)]
[HarmonyPatch("RimWorldRealFoW.Detours.DesignatorPrefix", "CanDesignateCell_Prefix")]
public static class Patch_DesignatorPrefix_CanDesignateCell_Prefix
{
    public static void Postfix(ref bool __result)
    {
        if (Command_FocusVehicleMap.FocusedVehicle is not null)
            __result = true;
    }
}