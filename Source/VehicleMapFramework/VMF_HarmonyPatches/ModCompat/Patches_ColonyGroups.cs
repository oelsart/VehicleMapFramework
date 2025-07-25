using HarmonyLib;
using System.Collections.Generic;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_ColonyGroups
{
    public const string Category = "VMF_Patches_ColonyGroups";

    static Patches_ColonyGroups()
    {
        if (ModCompat.ColonyGroups)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_ColonyGroups.Category)]
[HarmonyPatch("TacticalGroups.TacticalColonistBar", "CheckRecacheEntries")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TacticalColonistBar_CheckRecacheEntries
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_ColonistBar_CheckRecacheEntries.Transpiler(instructions);
    }
}
