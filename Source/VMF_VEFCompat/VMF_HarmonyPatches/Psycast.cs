using System.Collections.Generic;
using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VPsyE)]
[HarmonyPatch("VanillaPsycastsExpanded.Hediff_Overshield", "Tick")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Hediff_Overshield_Tick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.VPsyE)]
[HarmonyPatch("VanillaPsycastsExpanded.Hediff_Overshield", "DestroyProjectile")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Hediff_Overshield_DestroyProjectile
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.VPsyE)]
[HarmonyPatch("VanillaPsycastsExpanded.Hediff_Overshield", "CanDestroyProjectile")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Hediff_Overshield_CanDestroyProjectile
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}