using HarmonyLib;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_PauseOtherSettlements
{
    public const string Category = "VMF_Patches_PauseOtherSettlements";

    static Patches_PauseOtherSettlements()
    {
        if (PauseOtherSettlements)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_PauseOtherSettlements.Category)]
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