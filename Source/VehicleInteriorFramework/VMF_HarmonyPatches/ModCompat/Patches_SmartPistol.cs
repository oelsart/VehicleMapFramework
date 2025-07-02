using HarmonyLib;
using System.Collections.Generic;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_SmartPistol
{
    static Patches_SmartPistol()
    {
        if (ModCompat.SmartPistol)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_SmartPistol");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_SmartPistol")]
[HarmonyPatch("RB_SmartPistol.TargetCandidateUtil", "GetSubTargets")]
public static class Patch_TargetCandidateUtil_GetSubTargets
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory("VMF_Patches_SmartPistol")]
[HarmonyPatch("RB_SmartPistol.Mote_LockedCurve", "Tick")]
public static class Patch_Mote_LockedCurve_Tick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory("VMF_Patches_SmartPistol")]
[HarmonyPatch("RB_SmartPistol.Mote_LockedCurve", "DrawAt")]
public static class Patch_Mote_LockedCurve_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}