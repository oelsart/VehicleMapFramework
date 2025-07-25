using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_PRF
{
    public const string Category = "VMF_Patches_PRF";

    static Patches_PRF()
    {
        if (ModCompat.ProjectRimFactory)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_PRF.Category)]
[HarmonyPatch("ProjectRimFactory.Common.HarmonyPatches.Patch_CanReserve_SAL", "Postfix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_CanReserve_SAL_Postfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map)) - 1;
        codes[pos].opcode = OpCodes.Ldloc_2;
        return codes;
    }
}
