using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_CeleTech
{
  static Patches_CeleTech()
  {
    if (CeleTech)
    {
      VMF_Harmony.PatchCategory(PatchCategories.CeleTechArsenal);
      Patch_Projectile_CheckForFreeInterceptBetween.Prefixes.Add(Patch_CheckForFreeInterceptBetween_Prefix.PrefixPatch);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun", "OrderAttack")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_OrderAttack
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun", "IsTargetStillValid")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_IsTargetStillValid
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
        (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
        (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
        (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun", "TryFindNewTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_CMCTurretGun_TryFindNewTarget
{

  private static readonly List<IAttackTarget> tmpList = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Map), nameof(Map.attackTargetsCache))))
      .RemoveInstruction()
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.GetPotentialTargetsFor))))
      .Set(OpCodes.Call, ((Delegate)GetPotentialTargetsForCrossMap).Method)
      .InstructionEnumeration();
  }

  private static List<IAttackTarget> GetPotentialTargetsForCrossMap(Map map, IAttackTargetSearcher attackTargetSearcher)
  {
    tmpList.Clear();
    foreach (var map2 in map.BaseMapAndVehicleMaps(true))
    {
      tmpList.AddRange(map2.attackTargetsCache.GetPotentialTargetsFor(attackTargetSearcher));
    }
    return tmpList;
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun", "TestForTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_TestForTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun", "CanTargetNow")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_CanTargetNow
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
        (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
        (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun", "ScoreTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_ScoreTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMapSpawned)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMapSpawned)
      .MatchStartForward(
        new CodeMatch(OpCodes.Ldarg_0),
        CodeMatch.Calls(CachedMethodInfo.g_Thing_Map),
        CodeMatch.Calls(((Delegate)CoverUtility.CalculateOverallBlockChance).Method))
      .SetInstruction(CodeInstruction.LoadArgument(2))
      .Advance(-1)
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnAnotherThingMap)
      .Insert(CodeInstruction.LoadArgument(2))
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_CMCTurretGun_AAAS", "CanEngageTarget")]
[HarmonyPatch([typeof(LocalTargetInfo)])]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_AAAS_CanEngageTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Building_PDBattery", "TryFindNewTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_PDBattery_TryFindNewTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.AddAllBuildingsColonistForThingInstance();
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_PDBattery_TryFindNewTarget_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("CeleTech.Base.Building_PDBattery", "CeleTech.Base"), t => { return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")); });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CMCTurretTop", "DrawTurret")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CMCTurretTop_DrawTurret
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out _,
        getInstance:
        [
          CodeInstruction.LoadArgument(0),
          CodeInstruction.LoadField(
            GenTypes.GetTypeInAnyAssembly("CeleTech.Base.CMCTurretTop", "CeleTech.Base"),
            "parentTurret")
        ])
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CMCTurretTop", "ForceFaceTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_CMCTurretTop_ForceFaceTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CMCTurretTop", "TurretTopTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_CMCTurretTop_TurretTopTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Comp_FCradar", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_FCradar_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out _,
        getInstance: [CodeInstruction.LoadArgument(0), CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent))])
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Comp_CMCShield", "Draw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_CMCShield_Draw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Comp_PrismTowerTop", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_PrismTowerTop_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Comp_TraderShuttle", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_TraderShuttle_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Comp_UAV", "CompTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_UAV_CompTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Comp_FloatingGunRework", "CompTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_FloatingGunRework_CompTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_LauncherProjectileSwitchFire", "Retarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_Retarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
        (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
        (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
        (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_Verb_LauncherProjectileSwitchFire_Retarget_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("CeleTech.Base.Verb_LauncherProjectileSwitchFire", "CeleTech.Base"), t => { return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<Retarget>")); });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_LauncherProjectileSwitchFire", "CanHitFromCellIgnoringRange")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_CanHitFromCellIgnoringRange
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_Laser_Instant", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Instant_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_Laser_Instant_UAV", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Instant_UAV_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_Laser_Sustain", "BurstingTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Sustain_BurstingTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_Laser_Sustain", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Sustain_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_PlasmaIncinerator", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_PlasmaIncinerator_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_RocketShoot", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_RocketShoot_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_ShootDronePos", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootDronePos_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.Verb_Shoot_UAV", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Shoot_UAV_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.HarmonyPatches+Harmony_CheckForFreeInterceptBetween", "Prefix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_CheckForFreeInterceptBetween_Prefix
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool PrefixPatch(Projectile __instance, Vector3 lastExactPos, Vector3 newExactPos, ref bool __result)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CompFullProjectileInterceptor", "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompFullProjectileInterceptor_CheckIntercept
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
        (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
        (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CompFullProjectileInterceptor", "GetCurrentAlpha")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompFullProjectileInterceptor_GetCurrentAlpha
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CompFullProjectileInterceptor", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompFullProjectileInterceptor_PostDraw
{

  private static readonly HashSet<IAttackTarget> tmpSet = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Map), nameof(Map.attackTargetsCache))))
      .RemoveInstruction()
      .Set(OpCodes.Call, ((Delegate)TargetsHostileToColonyCrossMap).Method)
      .InstructionEnumeration();
  }

  private static HashSet<IAttackTarget> TargetsHostileToColonyCrossMap(Map map)
  {
    tmpSet.Clear();
    foreach (var map2 in map.BaseMapAndVehicleMaps(true))
    {
      tmpSet.AddRange(map2.attackTargetsCache.TargetsHostileToColony);
    }
    return tmpSet;
  }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("CeleTech.Base.CompFullProjectileInterceptor", "PostDrawExtraSelectionOverlays")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompFullProjectileInterceptor_PostDrawExtraSelectionOverlays
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_CompFullProjectileInterceptor_PostDraw.Transpiler(instructions);
  }
}
