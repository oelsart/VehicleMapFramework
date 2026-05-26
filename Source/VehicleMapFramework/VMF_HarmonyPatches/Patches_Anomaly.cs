using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Anomaly
{
    static Patches_Anomaly()
    {
        if (ModsConfig.AnomalyActive)
        {
            VMF_Harmony.PatchCategory(PatchCategories.Anomaly);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.Anomaly)]
[HarmonyPatch(typeof(Hediff_MetalhorrorImplant), nameof(Hediff_MetalhorrorImplant.Emerge))]
[PatchLevel(Level.Cautious)]
public static class Patch_Hediff_MetalhorrorImplant_Emerge
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
    }
}