using HarmonyLib;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_ReGrowth
{
    public const string Category = "VMF_Patches_ReGrowth";

    static Patches_ReGrowth()
    {
        if (ModCompat.ReGrowth)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_ReGrowth.Category)]
[HarmonyPatch("ReGrowthCore.MapComponent_SmartFarming", "FinalizeInit")]
public static class Patch_MapComponent_SmartFarming_FinalizeInit
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Isinst && c.OperandIs(typeof(PocketMapParent))));
        codes.InsertAfter(CodeInstruction.Call(typeof(Patch_MapComponent_SmartFarming_FinalizeInit), nameof(CheckNotVehicleMapParent)));
        return codes.Instructions();
    }

    private static PocketMapParent CheckNotVehicleMapParent(PocketMapParent mapParent)
    {
        if (mapParent is MapParent_Vehicle)
        {
            return null;
        }
        return mapParent;
    }
}
