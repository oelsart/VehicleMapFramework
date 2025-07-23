using HarmonyLib;
using System.Collections.Generic;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_SmartPistol
{
    public const string Category = "VMF_Patches_SmartPistol";

    static Patches_SmartPistol()
    {
        if (ModCompat.SmartPistol)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_SmartPistol.Category)]
[HarmonyPatch("RB_SmartPistol.TargetCandidateUtil", "GetSubTargets")]
public static class Patch_TargetCandidateUtil_GetSubTargets
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_SmartPistol.Category)]
[HarmonyPatch("RB_SmartPistol.Mote_LockedCurve", "Tick")]
public static class Patch_Mote_LockedCurve_Tick
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_SmartPistol.Category)]
[HarmonyPatch("RB_SmartPistol.Mote_LockedCurve", "DrawAt")]
public static class Patch_Mote_LockedCurve_DrawAt
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}