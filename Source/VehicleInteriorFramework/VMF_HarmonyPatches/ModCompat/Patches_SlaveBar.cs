using HarmonyLib;
using System.Collections.Generic;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_SlaveBar
    {
        static Patches_SlaveBar()
        {
            if (ModCompat.SlaveBar)
            {
                VMF_Harmony.PatchCategory("VMF_Patches_SlaveBar");
            }
        }
    }

    [HarmonyPatch("SlaveBar.Patches", "Patch_CheckRecacheEntries")]
    public static class Patch_Patches_Patch_CheckRecacheEntries
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_ColonistBar_CheckRecacheEntries.Transpiler(instructions);
    }
}
