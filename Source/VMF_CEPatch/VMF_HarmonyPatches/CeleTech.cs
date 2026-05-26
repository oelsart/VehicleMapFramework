using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CombatExtended;
using HarmonyLib;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class CeleTech
{
  static CeleTech()
  {
    if (ModCompat.CeleTech)
    {
      VMF_Harmony.PatchCategory(PatchCategories.CeleTechArsenalCECompat);
      Patch_BlockerRegistry_CheckForCollisionBetweenCallback.Prefixes.Add(
        Patch_Patch_ProjectileCE_CheckForCollisionBetween_Prefix.PrefixPatch);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenalCECompat)]
[HarmonyPatch("CeleTechXCE_DLL.HarmonyPatch_CMCProjectileChecker+Patch_ProjectileCE_CheckForCollisionBetween", "Prefix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Patch_ProjectileCE_CheckForCollisionBetween_Prefix
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool PrefixPatch(ProjectileCE __instance, ref bool __result)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenalCECompat)]
[HarmonyPatch("CeleTechXCE_DLL.HarmonyPatch_CMCProjectileChecker+Patch_ProjectileCE_CheckForCollisionBetween", "CheckIntercept_CMC")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Patch_ProjectileCE_CheckForCollisionBetween_CheckIntercept_CMC
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}
