using System.Collections.Generic;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AnimalCages
{
    static Patches_AnimalCages()
    {
        if (ModCompat.AnimalCages)
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
        var m_DepartMapOrMapHeld = AccessTools.Method(typeof(Patch_CageUtility_IsCaptiveOf), nameof(DepartMapOrMapHeld));
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, m_DepartMapOrMapHeld);
    }

    private static Map DepartMapOrMapHeld(Pawn pawn)
    {
        return pawn.DepartMap ?? pawn.MapHeld;
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