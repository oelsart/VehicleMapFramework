using System.Collections.Generic;
using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_SmartPistol
{
  static Patches_SmartPistol()
  {
    if (SmartPistol)
    {
      VMF_Harmony.PatchCategory(PatchCategories.SmartPistol);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.SmartPistol)]
[HarmonyPatch("RB_SmartPistol.TargetCandidateUtil", "GetSubTargets")]
[PatchLevel(Level.Cautious)]
public static class Patch_TargetCandidateUtil_GetSubTargets
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.SmartPistol)]
[HarmonyPatch("RB_SmartPistol.Mote_LockedCurve", "Tick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Mote_LockedCurve_Tick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.SmartPistol)]
[HarmonyPatch("RB_SmartPistol.Mote_LockedCurve", "DrawAt")]
[PatchLevel(Level.Cautious)]
public static class Patch_Mote_LockedCurve_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}
