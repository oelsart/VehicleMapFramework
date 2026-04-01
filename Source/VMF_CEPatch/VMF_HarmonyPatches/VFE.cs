using System.Collections.Generic;
using CombatExtended.Compatibility;
using HarmonyLib;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_CE_VFECompat
{
    static Patches_CE_VFECompat()
    {
        if (ModCompat.VFESecurity.Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.CombatExtendedVFECompat);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedVFECompat)]
[HarmonyPatch(typeof(VanillaExpandedFramework), "ShieldZonesCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_VanillaExpandedFramework_ShieldZonesCallback
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedVFECompat)]
[HarmonyPatch(typeof(VanillaExpandedFramework), "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_VanillaExpandedFramework_CheckIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}