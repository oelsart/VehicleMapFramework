using HarmonyLib;
using System.Collections.Generic;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_MultiFloors
{
    public const string Category = "VMF_Patches_MultiFloors";

    static Patches_MultiFloors()
    {
        if (ModCompat.MultiFloors.Active)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatch("MultiFloors.Stair", "Print")]
public static class Patch_Stair_Print
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Thing_Print.Transpiler(instructions);
}