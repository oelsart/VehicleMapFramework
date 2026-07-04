using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_DefensiveNetwork
{
  static Patches_DefensiveNetwork()
  {
    if (DefensiveNetwork.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.DefensiveNetwork);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_HunterKillerSupportSystem", "DrawSupportOverlay")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_HunterKillerSupportSystem_DrawSupportOverlay
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out _, 0.1f)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_HunterKillerSupportSystem", "CallSupport")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_HunterKillerSupportSystem_CallSupport
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_HunterKillerSupportSystem", "DrawTargetArea")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_HunterKillerSupportSystem_DrawTargetArea
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_HunterKillerSupportSystem_TargetingParameters_Delegate
{
  private static MethodBase TargetMethod()
  {
    return GenTypes.GetTypeInAnyAssembly("DNX.Building_HunterKillerSupportSystem", "DNX")
      .GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<get_TargetingParameters>"));
  }
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_GhoulBomberBay", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_GhoulBomberBay_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out var vehicle)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity))
      .Advance()
      .AddExtraAngle(vehicle)
      .InstructionEnumeration();
  }
}


[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_GhoulBomberBay", "LaunchStrike")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_GhoulBomberBay_LaunchStrike
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_GhoulBomberBay", "DrawTargetArea")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_GhoulBomberBay_DrawTargetArea
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_GhoulBomberBay", "DrawHeavyBombTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_GhoulBomberBay_DrawHeavyBombTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_GhoulBomberBay_TargetingParameters_Delegate
{
  private static MethodBase TargetMethod()
  {
    return GenTypes.GetTypeInAnyAssembly("DNX.Building_GhoulBomberBay", "DNX")
      .GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<get_TargetingParameters>"));
  }
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompTargeter_IsValidTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method("DNX.CompSpecialGrenadeLauncher:IsValidTarget");
    yield return AccessTools.Method("DNX.CompManualMissileLauncher:IsValidTarget");
    yield return AccessTools.Method("DNX.CompLongbowManualTargeter:IsValidTarget");
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_BuildingTargeter_IsValidTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method("DNX.Building_TacticalMarkerTower:IsValidTarget");
    yield return AccessTools.Method("DNX.Building_SustainedLaserEmitter:IsValidTarget", [typeof(Pawn), typeof(float)]);
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DNXTurretGun", "TryFindMultispectralTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DNXTurretGun_TryFindMultispectralTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DNXTurretGun", "IsValidMultispectralTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DNXTurretGun_IsValidMultispectralTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DNXTurretGun", "ScoreMultispectralTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DNXTurretGun_ScoreMultispectralTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_WatcherPrecisionTurret", "FindPreferredTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_WatcherPrecisionTurret_FindPreferredTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_WatcherPrecisionTurret_IsUsableTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var type = GenTypes.GetTypeInAnyAssembly("DNX.Building_WatcherPrecisionTurret", "DNX");
    return AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "IsUsableTarget");
  }
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_WatcherPrecisionTurret", "TargetAngle")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_WatcherPrecisionTurret_TargetAngle
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.WatcherTargetingUtility", "CountWatchersTargeting")]
[PatchLevel(Level.Safe)]
public static class Patch_WatcherTargetingUtility_CountWatchersTargeting
{
  private static bool working;
  
  public static void Postfix(Map map, Thing target, Building ignored, ref int __result)
  {
    if (working) return;
    working = true;
    try
    {
      foreach (var map2 in map.BaseMapAndVehicleMaps(false))
      {
        __result += (int)DefensiveNetwork.CountWatchersTargeting(null,
          Params<(object, object, object)>.Get((map2, target, ignored)));
      }
    }
    finally
    {
      working = false;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_AlignedTrackingShot", "CanEngage")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_AlignedTrackingShot_CanEngage
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_AlignedTrackingShot", "IsTurretAligned")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_AlignedTrackingShot_IsTurretAligned
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_LaserChargeShot", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaserChargeShot_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_LaserChargeShot", "ThrowImpactBeam")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaserChargeShot_ThrowImpactBeam
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_LongbowRocketBarrage", "TargetInLongbowRange")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LongbowRocketBarrage_TargetInLongbowRange
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_LongbowRocketBarrage", "ScatteredTargetCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LongbowRocketBarrage_ScatteredTargetCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_ShootWithAimingExtensionRange", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithAimingExtensionRange_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_ShootWithAimingExtensionRange", "BuildBurstTargets")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithAimingExtensionRange_BuildBurstTargets
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap),
      (CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_ShootWithAimingExtensionRange", "IsValidBurstTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithAimingExtensionRange_IsValidBurstTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_ShootWithAimingExtensionRange", "ThrowWarmupMotes")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithAimingExtensionRange_ThrowWarmupMotes
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_ShootWithAimingExtensionRange", "RandomMissTargetNear")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithAimingExtensionRange_RandomMissTargetNear
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Verb_ShootWithAimingExtensionRange", "MuzzleWorldPosition")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithAimingExtensionRange_MuzzleWorldPosition
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_EDD", "DrawExtraSelectionOverlays")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_EDD_DrawExtraSelectionOverlays
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenDraw_DrawFieldEdges2))
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CachedMethodInfo.g_Thing_Map.CallvirtInstruction)
      .SetOperandAndAdvance(CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges2)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_EDD", "DrawTripLaser")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_EDD_DrawTripLaser
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
      .InsertAfter(
        CodeInstruction.LoadArgument(0),
        CachedMethodInfo.g_Thing_Map.CallvirtInstruction,
        CachedMethodInfo.m_ToBaseMapCoord3.CallInstruction)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DirectionalShieldGenerator", "RefreshCoveredCells")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_DirectionalShieldGenerator_RefreshCoveredCells
{
  public static void Postfix(Building __instance, List<IntVec3> ___coveredCells)
  {
    if (__instance.IsOnVehicleMapOf(out var vehicle))
    {
      for (var i = 0; i < ___coveredCells.Count; i++)
      {
        ___coveredCells[i] = ___coveredCells[i].ToBaseMapCoord(vehicle);
      }
    }
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DirectionalShieldGenerator", "TryAbsorbIncomingProjectiles")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DirectionalShieldGenerator_TryAbsorbIncomingProjectiles
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DirectionalShieldGenerator", "ShouldAbsorb")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DirectionalShieldGenerator_ShouldAbsorb
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotationSpawned_Thing),
      (CachedMethodInfo.g_Rot4_FacingCell, CachedMethodInfo.g_Rot8_FacingCell));
  }
}

[HarmonyPatchCategory(PatchCategories.DefensiveNetwork)]
[HarmonyPatch("DNX.Building_DirectionalShieldGenerator", "Absorb")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DirectionalShieldGenerator_Absorb
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap));
  }
}