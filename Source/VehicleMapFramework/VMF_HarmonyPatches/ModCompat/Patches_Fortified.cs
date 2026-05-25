using System.Collections.Generic;
using HarmonyLib;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_Fortified
{
  static Patches_Fortified()
  {
    if (Fortified)
    {
      VMF_Harmony.PatchCategory(PatchCategories.FortifiedFeaturesFramework);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.FortifiedFeaturesFramework)]
[HarmonyPatch("Fortified.Verb_CastAbilityArcSprayProjectile", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityArcSprayProjectile_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.FortifiedFeaturesFramework)]
[HarmonyPatch("Fortified.Verb_CastAbilityArcSprayProjectile", "PreparePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityArcSprayProjectile_PreparePath
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_Verb_ArcSpray_PreparePath.Transpiler(instructions);
  }
}
