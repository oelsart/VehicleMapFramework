using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_Vivi
{
  static Patches_Vivi()
  {
    if (Vivi)
    {
      VMF_Harmony.PatchCategory(PatchCategories.ViviRace);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ViviRace)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_ArcanePlant_Turret_TryFindNewTarget_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("VVRace.ArcanePlant_Turret"),
      t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.ViviRace)]
[HarmonyPatch("VVRace.ArcanePlant_Turret", "TryFindNewTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ArcanePlant_Turret_TryFindNewTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.AddAllBuildingsColonistForThingInstance();
  }
}
