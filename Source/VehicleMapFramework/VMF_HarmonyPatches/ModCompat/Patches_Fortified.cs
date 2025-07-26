using HarmonyLib;
using System.Collections.Generic;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Fortified
{
    public const string Category = "VMF_Patches_DMS";

    static Patches_Fortified()
    {
        if (ModCompat.Fortified)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_Fortified.Category)]
[HarmonyPatch("Fortified.Verb_CastAbilityArcSprayProjectile", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityArcSprayProjectile_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_Fortified.Category)]
[HarmonyPatch("Fortified.Verb_CastAbilityArcSprayProjectile", "PreparePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityArcSprayProjectile_PreparePath
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_Verb_ArcSpray_PreparePath.Transpiler(instructions);
    }
}