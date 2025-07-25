using HarmonyLib;
using System.Collections.Generic;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_DMS
{
    public const string Category = "VMF_Patches_DMS";

    static Patches_DMS()
    {
        if (ModCompat.DeadMansSwitch)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_DMS.Category)]
[HarmonyPatch("DMS.Verb_CastAbilityArcSprayProjectile", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityArcSprayProjectile_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_DMS.Category)]
[HarmonyPatch("DMS.Verb_CastAbilityArcSprayProjectile", "PreparePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityArcSprayProjectile_PreparePath
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_Verb_ArcSpray_PreparePath.Transpiler(instructions);
    }
}