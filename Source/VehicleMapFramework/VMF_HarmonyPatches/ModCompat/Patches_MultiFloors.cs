using HarmonyLib;
using System.Collections.Generic;
using Verse;

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

[HarmonyPatch("MultiFloors.StairExit", "Print")]
public static class Patch_StairExit_Print
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Thing_Print.Transpiler(instructions);
}

[HarmonyPatch("MultiFloors.Maps.LevelMapGenerator", "SetupMapGenerator")]
public static class Patch_LevelMapGenerator_SetupMapGenerator
{
    public static void Postfix(Thing entrance, ref MapGeneratorDef __result)
    {
        if (entrance.IsOnVehicleMapOf(out _) && __result?.defName == "MF_Basement")
        {
            var MF_BasementWithoutCaves = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("MF_BasementWithoutCaves");
            if (MF_BasementWithoutCaves != null)
            {
                __result = MF_BasementWithoutCaves;
            }
        }
    }
}