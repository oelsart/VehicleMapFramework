using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_ColonyGroups
    {
        static Patches_ColonyGroups()
        {
            if (ModsConfig.IsActive("DerekBickley.LTOColonyGroupsFinal"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_ColonyGroups");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_ColonyGroups")]
    [HarmonyPatch("TacticalGroups.TacticalColonistBar", "CheckRecacheEntries")]
    public static class Patch_TacticalColonistBar_CheckRecacheEntries
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_ColonistBar_CheckRecacheEntries.Transpiler(instructions);
        }
    }
}
