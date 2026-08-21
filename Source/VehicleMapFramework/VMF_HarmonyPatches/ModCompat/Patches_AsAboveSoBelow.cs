using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat.AsAboveSoBelow;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AsAboveSoBelow
{
  static Patches_AsAboveSoBelow()
  {
    if (AsAboveSoBelow.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.AsAboveSoBelow);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyAfter(HarmonyId)]
[HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
[PatchLevel(Level.Safe)]
public static class Patch_MapGenerator_GenerateMap
{
  public static void Prefix(ref IntVec3 mapSize, MapParent parent)
  {
    if (parent is MapParent_Vehicle { Faction.IsPlayer: true })
    {
      var pendingLayout = PendingLayout.Invoke(null);
      var count = UpperLevels() + 1;
      bandCount.SetValue(pendingLayout, count);
      bandHeight.SetValue(pendingLayout, mapSize.z);
      // surfaceBandは0 (default)
      pending.SetValue(null, pendingLayout);
      mapSize = new IntVec3(mapSize.x, mapSize.y, count * SlotFor(mapSize.z));
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Building_ABStairs2", "CarveLanding")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_ABStairs2_CarveLanding
{
  public static void Postfix(Map map, MapComponent bands, int targetBand)
  {
    if (map.IsVehicleMapOf(out var vehicle) &&
        Banded(bands))
    {
      vehicle.SpawnStructures(RectOfBand(map, targetBand).Min);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Patch_CameraDriver_ABClampToBand", "Postfix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_CameraDriver_ABClampToBand_Postfix
{
  public static bool Prefix() => !Find.CurrentMap.IsVehicleMap || !VehicleMapFramework.settings.drawPlanet;
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.SectionLayer_ABBelowV2", "MaterialFor")]
[PatchLevel(Level.Safe)]
public static class Patch_SectionLayer_ABBelowV2_MaterialFor
{
  public static void Postfix(Map map, ref Material __result)
  {
    if (map.IsVehicleMap)
    {
      __result = SectionLayer_TerrainOnVehicle.GetMaterialWithZ(__result);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.ABBandView", "TryStep")]
[PatchLevel(Level.Safe)]
public static class PatchABBandView_TryStep
{
  public static void Prefix(ref Map map)
  {
    var selected = Find.Selector.SingleSelectedObject;
    if (selected is VehiclePawnWithMap vehicle ||
        selected is Thing thing && thing.IsOnVehicleMapOf(out vehicle) ||
        selected is Zone zone && zone.Map.IsVehicleMapOf(out vehicle))
      map = vehicle.CurrentLevel;
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.ABBandView", "SetBand")]
[PatchLevel(Level.Safe)]
public static class PatchABBandView_SetBand
{
  public static void Prefix(Map map, ref bool preserveXZ)
  {
    if (preserveXZ && map.IsNonFocusedVehicleMap)
    {
      // 車両マップの切り替え時はカメラをパンさせない
      preserveXZ = false;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Patch_JobTracker_ABLocalizeJobLines", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_JobTracker_ABLocalizeJobLines_Prefix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(LocalizeForPawn.Method))
      .SetOperandAndAdvance(((Delegate)LocalizeForPawnIfNotOnVehicleMap).Method)
      .MatchStartForward(CodeMatch.Calls(LocalizeForPawn.Method))
      .Repeat(c => c
        .Advance(-1)
        .RemoveInstruction()
        .SetOperandAndAdvance(((Delegate)LocalizeForPawnTarget).Method))
      .Reset()
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Repeat(c => c.Set(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing))
      .InstructionEnumeration();
  }

  private static Vector3 LocalizeForPawnIfNotOnVehicleMap(Pawn pawn, Vector3 world)
  {
    return pawn.IsOnNonFocusedVehicleMap
      ? world
      : LocalizeForPawn(pawn, world);
  }

  private static Vector3 LocalizeForPawnTarget(Pawn pawn, ref LocalTargetInfo target)
  {
    if (target.Thing.IsOnNonFocusedVehicleMap ||
        pawn.stances.curStance is Stance_Busy && pawn.TargetMap.IsNonFocusedVehicleMap ||
        pawn.CurJob is { globalTarget.Map.IsNonFocusedVehicleMap: true } ||
        pawn.CurJob?.GetCachedDriver(pawn) is JobDriverAcrossMaps { DestMap.IsNonFocusedVehicleMap: true } ||
        pawn.IsOnNonFocusedVehicleMap && pawn.stances.curStance is not Stance_Busy { verb: Verb_Jump or Verb_CastAbilityJump })
    {
      return Patch_Pawn_JobTracker_DrawLinesBetweenTargets.CenterVector3VehicleOffset(ref target, pawn);
    }
    return LocalizeForPawn(pawn, target.CenterVector3);
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Patch_PawnPath_ABLiftPathLine", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_PawnPath_ABLiftPathLine_Prefix
{
  public static bool Prefix(Pawn pathingPawn, ref bool __result)
  {
    if (pathingPawn.IsOnNonFocusedVehicleMap)
    {
      __result = true;
      return false;
    }
    return true;
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Patch_ShotReport_ABCrossBandDistance", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_ShotReport_ABCrossBandDistance_Prefix
{
  public static bool Prefix(Thing caster, LocalTargetInfo target)
  {
    return !caster.IsOnNonFocusedVehicleMap && !target.Thing.IsOnNonFocusedVehicleMap ||
           caster.Map == (target.Thing?.Map ?? caster.GroundMap);
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.Patch_Projectile_ABCrossBandOrigin", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_Projectile_ABCrossBandOrigin_Prefix
{
  public static bool Prefix(Thing launcher, LocalTargetInfo usedTarget)
  {
    return Patch_Patch_ShotReport_ABCrossBandDistance_Prefix.Prefix(launcher, usedTarget);
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelow)]
[HarmonyPatch("AsAboveSoBelow.ABCombatAim", "TryLocalAngle")]
[PatchLevel(Level.Safe)]
public static class Patch_ABCombatAim_TryLocalAngle
{
  public static bool Prefix(Building_Turret turret, LocalTargetInfo target)
  {
    return Patch_Patch_ShotReport_ABCrossBandDistance_Prefix.Prefix(turret, target);
  }
}