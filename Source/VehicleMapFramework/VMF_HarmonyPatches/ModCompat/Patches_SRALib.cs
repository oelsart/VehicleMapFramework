using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_SRALib
{
  public static readonly List<Type> t_Building_TurretGunHasSpeed;

  static Patches_SRALib()
  {
    if (SRALib)
    {
      t_Building_TurretGunHasSpeed =
        GenTypes.AllTypes.Where(t => t.Name == "Building_TurretGunHasSpeed").ToList();
      VMF_Harmony.PatchCategory(PatchCategories.SRALib);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunHasSpeed_IsValidTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return Patches_SRALib.t_Building_TurretGunHasSpeed.Select(t => AccessTools.Method(t, "IsValidTarget"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunHasSpeed_TryFindNewTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return Patches_SRALib.t_Building_TurretGunHasSpeed.Select(t => AccessTools.Method(t, "TryFindNewTarget"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.AddAllBuildingsColonistForThingInstance();
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunHasSpeed_TryFindNewTarget_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return Patches_SRALib.t_Building_TurretGunHasSpeed.Select(t =>
    {
      return AccessTools.FindIncludingInnerTypes(t,
        t2 => { return t2.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")); });
    });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Projectile_BulletWithEffect_Impact
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method("SRA.Projectile_BulletWithEffect:Impact");
    yield return AccessTools.Method("SRA.Projectile_BeamWithEffect:Impact");
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "AffectedCells")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_AffectedCells
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "TargetPosition")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_TargetPosition
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "CanUseCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_CanUseCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_ShootWithOffset", "BaseTryCastShot")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootWithOffsetSRA_BaseTryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}