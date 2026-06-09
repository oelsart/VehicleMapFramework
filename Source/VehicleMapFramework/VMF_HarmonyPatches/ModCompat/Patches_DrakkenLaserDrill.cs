using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_DrakkenLaserDrill
{
  static Patches_DrakkenLaserDrill()
  {
    if (DrakkenLaserDrill)
    {
      VMF_Harmony.PatchCategory(PatchCategories.DrakkenLaserDrill);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_MouseAttack", "DoSomething")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_MouseAttack_DoSomething
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_MouseAttack", "DoSomething_Move")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_MouseAttack_DoSomething_Move
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_AutoAttack", "DoSomething_AttackAllPawn")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_AutoAttack_DoSomething_AttackAllPawn
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_AutoAttack", "PrepareToAttack")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_AutoAttack_PrepareToAttack
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "DoSomething_I")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_I
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_I_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var type = GenTypes.GetTypeInAnyAssembly("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "MYDE_DrakkenLaserDrill");
    return AccessTools.InnerTypes(type)
      .SelectMany(t => t.GetDeclaredMethods())
      .Where(m => m.Name.Contains("<DoSomething_I>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    if (UnitTestDetector.IsTestingContext) return instructions;
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_TargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned_TargetInfo));
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "DoSomething_II")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_II
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DrakkenLaserDrill)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_II_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var type = GenTypes.GetTypeInAnyAssembly("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "MYDE_DrakkenLaserDrill");
    return AccessTools.InnerTypes(type)
      .SelectMany(t => t.GetDeclaredMethods())
      .Where(m => m.Name.Contains("<DoSomething_II>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    if (UnitTestDetector.IsTestingContext) return instructions;
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_TargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned_TargetInfo),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}
