using System.Collections.Generic;
using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_ColonyGroups
{
    static Patches_ColonyGroups()
    {
        if (ColonyGroups)
        {
            VMF_Harmony.PatchCategory(PatchCategories.ColonyGroups);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.ColonyGroups)]
[HarmonyPatch("TacticalGroups.TacticalColonistBar", "CheckRecacheEntries")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TacticalColonistBar_CheckRecacheEntries
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_ColonistBar_CheckRecacheEntries.Transpiler(instructions);
    }
}
