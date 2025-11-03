using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_BiomesCaverns
{
    static Patches_BiomesCaverns()
    {
        if (ModCompat.BiomesCaverns)
        {
            VMF_Harmony.PatchCategory(PatchCategories.BiomesCaverns);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.BiomesCaverns)]
[HarmonyPatch("Caveworld_Flora_Unleashed.MapComponent_CaveFungus", "MapComponentTick")]
[PatchLevel(Level.Sensitive)]
public static class Patch_MapComponent_CaveFungus_MapComponentTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var instruction = instructions.FirstOrDefault(c => c.opcode == OpCodes.Ldc_I4_1);
        if (instruction != null)
        {
            instruction.opcode = OpCodes.Ldc_I4_S;
            instruction.operand = 100;
        }
        return instructions;
    }
}