using HarmonyLib;
using System.Collections.Generic;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_YayosCombat3
{
    public const string Category = "VMF_Patches_YayosCombat3";

    static Patches_YayosCombat3()
    {
        if (ModCompat.YayosCombat3)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_YayosCombat3.Category)]
[HarmonyPatch("yayoCombat.HarmonyPatches.Verb_LaunchProjectile_TryCastShot", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectile_TryCastShot_Prefix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}