using HarmonyLib;
using System.Collections.Generic;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_ColonyGroups
    {
        static Patches_ColonyGroups()
        {
            if (ModCompat.ColonyGroups)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_ColonyGroups");

                VMF_Harmony.Instance.Patch(AccessTools.Method("TacticalGroups.TacticalColonistBar:CheckRecacheEntries"), transpiler: AccessTools.Method(typeof(Patch_TacticalColonistBar_CheckRecacheEntries), nameof(Patch_TacticalColonistBar_CheckRecacheEntries.Transpiler)));
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
