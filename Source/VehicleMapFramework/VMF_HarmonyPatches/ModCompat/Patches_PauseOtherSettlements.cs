using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_PauseOtherSettlements
{
    static Patches_PauseOtherSettlements()
    {
        if (PauseOtherSettlements)
        {
            VMF_Harmony.PatchCategory(PatchCategories.PauseOtherSettlements);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PauseOtherSettlements)]
[HarmonyPatch("PauseOtherSettlementsSimulation.PauseOtherSettlementsSimulation", "ShouldSimulateMap")]
public static class Patch_PauseOtherSettlementsSimulation_ShouldSimulateMap
{
    public static bool Prefix(ref Map map, ref bool __result)
    {
        if (map.IsVehicleMapOf(out var vehicle) && !vehicle.Spawned)
        {
            __result = true;
            return false;
        }
        map = map.BaseMap();
        return true;
    }
}