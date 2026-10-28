using System.Reflection;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch("VanillaGravshipExpanded.CompPointDefence", "FindTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefence_FindTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompPointDefence_FindTarget_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return GenTypes.GetTypeInAnyAssembly("VanillaGravshipExpanded.CompPointDefence", "VanillaGravshipExpanded")
      .GetDeclaredMethods()
      .Where(m =>
      {
        if (!m.Name.Contains("<FindTarget>")) return false;
        return PatchHelper.ReadMethodBodyWrapper(m).Any(i =>
          CachedMethodInfo.g_Thing_Position.Equals(i.Value));
      });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch("VanillaGravshipExpanded.CompPointDefence", "TryIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefence_TryIntercept
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}