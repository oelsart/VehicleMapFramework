using System.Collections.Generic;
using HarmonyLib;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_Gunplay
{
    static Patches_Gunplay()
    {
        if (ModCompat.Gunplay)
        {
            VMF_Harmony.PatchCategory(PatchCategories.Gunplay);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.Gunplay)]
[HarmonyPatch("Gunplay.Patch.PatchProjectileLaunch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_PatchProjectileLaunch_Postfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}
