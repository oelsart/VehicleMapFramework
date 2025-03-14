using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_BiomesCaverns
    {
        static Patches_BiomesCaverns()
        {
            if (ModsConfig.IsActive("BiomesTeam.BiomesCaverns"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_BiomesCaverns");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_BiomesCaverns")]
    [HarmonyPatch("Caveworld_Flora_Unleashed.MapComponent_CaveFungus", "MapComponentTick")]
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
}