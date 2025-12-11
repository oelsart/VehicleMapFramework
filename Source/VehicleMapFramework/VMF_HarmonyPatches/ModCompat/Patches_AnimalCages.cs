using System.Collections.Generic;
using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AnimalCages
{
    static Patches_AnimalCages()
    {
        if (AnimalCages)
        {
            VMF_Harmony.PatchCategory(PatchCategories.AnimalCages);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.AnimalCages)]
[HarmonyPatch("AnimalCage.CageUtility", "IsCaptiveOf")]
[PatchLevel(Level.Cautious)]
public static class Patch_CageUtility_IsCaptiveOf
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_DepartMapOrPawnMapHeld);
    }
}

[HarmonyPatchCategory(PatchCategories.AnimalCages)]
[HarmonyPatch("AnimalCage.CageUtility", "CurrentCage")]
[PatchLevel(Level.Cautious)]
public static class Patch_CageUtility_CurrentCage
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Patch_CageUtility_IsCaptiveOf.Transpiler(instructions);
}