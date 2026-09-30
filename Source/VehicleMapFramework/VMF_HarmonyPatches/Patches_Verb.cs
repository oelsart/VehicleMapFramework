using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_TryFindShootLineFromTo
{
  private static bool Prepare()
  {
    return !CombatExtended;
  }

  public static bool Prefix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine,
    bool ignoreRange, ref bool __result)
  {
    if (VerbOnVehicleUtility.ShouldConsiderCrossMap(__instance.caster, root, targ))
    {
      __result = __instance.TryFindShootLineFromToOnVehicle(root, targ, out resultingLine, ignoreRange);
      return false;
    }

    return true;
  }
}

//CanHitTargetFrom内でrootとターゲットとの距離を測ってたりする時用（Jumpなど）
[HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CanHitTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "GetForcedMissTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectile_GetForcedMissTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell,
      CachedMethodInfo.m_CellOnBaseMapSpawned);
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_LaunchProjectile_GetForcedMissTarget_Delegate
{
  private static MethodInfo TargetMethod()
  {
    return typeof(Verb_LaunchProjectile).GetDeclaredMethods().First(m => m.Name.Contains("<GetForcedMissTarget>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class Patch_Verb_LaunchProjectile_TryCastShot
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(Thing ___caster)
  {
    if (___caster.IsOnVehicleMapOf(out var vehicle) && (!vehicle.Spawned || vehicle.VehicleMap == Find.CurrentMap))
    {
      VehiclePawnWithMapCache.CacheMode = true;
    }
  }

  [PatchLevel(Level.Safe)]
  public static void Finalizer() => VehiclePawnWithMapCache.CacheMode = false;

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool),
  typeof(bool), typeof(bool), typeof(bool))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_TryStartCastOn
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "TryGetHitCell")]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_ShootBeam_TryGetHitCell
{
  public static bool Prefix(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell, Thing ___caster,
    VerbProperties ___verbProps, out bool __result)
  {
    var intVec =
      GenSight.LastPointOnLineOfSight(source, targetCell, c => c.CanBeSeenOverOnVehicle(___caster.BaseMap()), true);
    if (___verbProps.beamCantHitWithinMinRange && intVec.DistanceTo(source) < ___verbProps.minRange)
    {
      hitCell = default;
      __result = false;
      return false;
    }

    hitCell = intVec.IsValid ? intVec : targetCell;
    __result = intVec.IsValid;
    return false;
  }
}

[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_GetBeamHitNeighbourCells
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(typeof(Verb_ShootBeam), t =>
      !t.Name.Contains("<GetBeamHitNeighbourCells>") ? null : AccessTools.Method(t, "MoveNext"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1));
  }
}

[HarmonyPatch(typeof(Verb_ShootBeam), nameof(Verb_ShootBeam.BurstingTick))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_BurstingTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootBeam_BurstingTick_Delegate
{
  private static MethodInfo TargetMethod()
  {
    return typeof(Verb_ShootBeam).GetDeclaredMethods().First(m => m.Name.Contains("<BurstingTick>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_CanBeSeenOverFast, CachedMethodInfo.m_CanBeSeenOverOnVehicleFast));
  }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "CalculatePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_CalculatePath
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "HitCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_HitCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "ApplyDamage")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_ApplyDamage
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootBeam_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return typeof(Verb_ShootBeam).FindIncludingInnerTypes(t =>
      t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<ApplyDamage>")));
    yield return typeof(Verb_ShootBeam).FindIncludingInnerTypes(t =>
      t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<BurstingTick>")));
    yield return typeof(Verb_ShootBeam).FindIncludingInnerTypes(t =>
      t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryGetHitCell>")));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_CanBeSeenOverFast, CachedMethodInfo.m_CanBeSeenOverOnVehicleFast));
  }
}

[HarmonyPatch(typeof(Verb_Spray), "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Spray_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(Verb_ArcSpray), "PreparePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ArcSpray_PreparePath
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatch(typeof(Verb_ArcSprayProjectile), "HitCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ArcSprayProjectile_HitCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.CanHitTargetFrom))]
[PatchLevel(Level.Sensitive)]
public static class Patch_JumpUtility_CanHitTargetFrom
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_TargetCellOnBaseMap),
      (CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1));

    var pos = codes.FindIndex(c => c.Calls(CachedMethodInfo.m_TargetCellOnBaseMap));
    codes.Insert(pos, CodeInstruction.LoadArgument(0));
    return codes;
  }
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.OrderJump))]
[PatchLevel(Level.Cautious)]
public static class Patch_JumpUtility_OrderJump
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_ThingTargetMap);
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_JumpUtility_OrderJump_Delegate
{
  public static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(JumpUtility),
      t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<OrderJump>")));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatch(typeof(JobDriver_CastJump), nameof(JobDriver_CastJump.TryMakePreToilReservations))]
[PatchLevel(Level.Cautious)]
public static class Patch_JobDriver_CastJump_TryMakePreToilReservations
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_PawnTargetMap);
  }
}

[HarmonyPatch(typeof(PawnFlyer), nameof(PawnFlyer.SpawnSetup))]
[PatchLevel(Level.Safe)]
public static class Patch_PawnFlyer_SpawnSetup
{
  public static void Prefix(Map map, Vector3 ___startVec, IntVec3 ___destCell, ref float ___flightDistance)
  {
    ___flightDistance = ___destCell.ToBaseMapCoord(map).DistanceTo(___startVec.ToIntVec3());
  }
}

[HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.DrawHighlight))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_Jump_DrawHighlight
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Set(OpCodes.Call, CachedMethodInfo.m_ThingTargetMap)
      .MatchStartForward(
        CodeMatch.Calls(AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3))))
      .InsertAndAdvance(CodeInstruction.LoadArgument(0))
      .Set(OpCodes.Call, ((Delegate)CenterVector3Offset).Method)
      .InstructionEnumeration();
  }

  public static Vector3 CenterVector3Offset(ref LocalTargetInfo target, Verb verb)
  {
    var caster = verb.caster;

    var thing = target.Thing;
    Map map;
    if (thing != null)
    {
      if (thing.Spawned)
      {
        return thing.DrawPos;
      }

      if (thing.SpawnedOrAnyParentSpawned)
      {
        return caster.TryGetTargetMap(out map)
          ? thing.PositionHeld.ToVector3Shifted().ToBaseMapCoord(map)
          : thing.PositionHeld.ToVector3Shifted();
      }

      return caster.TryGetTargetMap(out map)
        ? thing.Position.ToVector3Shifted().ToBaseMapCoord(map)
        : thing.Position.ToVector3Shifted();
    }

    var cell = target.Cell;
    if (!cell.IsValid) return default;
    return caster.TryGetTargetMap(out map)
      ? cell.ToVector3Shifted().ToBaseMapCoord(map)
      : cell.ToVector3Shifted();
  }
}

[HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.DrawHighlight))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityJump_DrawHighlight
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
    Patch_Verb_Jump_DrawHighlight.Transpiler(instructions);
}

[HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.OnGUI))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Jump_OnGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_ThingTargetMap);
  }
}

[HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.OnGUI))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityJump_OnGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
    Patch_Verb_Jump_OnGUI.Transpiler(instructions);
}

[HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.ValidateTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Jump_ValidateTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_PawnTargetMap);
  }
}

[HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.ValidateTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityJump_ValidateTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
    Patch_Verb_Jump_ValidateTarget.Transpiler(instructions);
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.ValidJumpTarget))]
[PatchLevel(Level.Safe)]
public static class Patch_JumpUtility_ValidJumpTarget
{
  private static bool working;
  
  public static void Postfix(Thing flying, Map map, IntVec3 cell, ref bool __result)
  {
    if (__result || working)
      return;

    working = true;
    try
    {
      if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
      {
        if (vehicle.Spawned)
        {
          __result = JumpUtility.ValidJumpTarget(flying, vehicle.Map, cell.ToBaseMapCoord(vehicle));
          return;
        }

        var point = cell.ToVector3Shifted().ToBaseMapCoord(vehicle);
        if (point.TryGetVehicleMap(Find.CurrentMap, out var vehicle2))
        {
          __result = JumpUtility.ValidJumpTarget(flying, vehicle2.VehicleMap, point.ToVehicleMapCoord(vehicle2).ToIntVec3());
        }

        return;
      }
      if (cell.TryGetVehicleMap(map, out var vehicle3))
      {
        __result = JumpUtility.ValidJumpTarget(flying, vehicle3.VehicleMap, cell.ToVehicleMapCoord(vehicle3));
      }
    }
    finally
    {
      working = false;
    }
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_Jump_DrawHighlight_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return typeof(Verb_Jump).GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawHighlight>"));
    yield return typeof(Verb_CastAbilityJump).GetDeclaredMethods()
      .FirstOrDefault(m => m.Name.Contains("<DrawHighlight>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1);
  }
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.DoJump))]
[PatchLevel(Level.Cautious)]
public static class Patch_JumpUtility_DoJump
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_PawnTargetMap);
  }
}

[HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.DrawRadiusRing))]
[PatchLevel(Level.Safe)]
public static class Patch_VerbProperties_DrawRadiusRing
{
  internal static void Prefix(Verb verb, [MustDisposeResource] ref FocusMapScope __state)
  {
    if (verb.caster.IsOnNonFocusedVehicleMapOf(out var vehicle))
      __state = FocusMapScope.FocusMapUnsafe(vehicle.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatch(typeof(Targeter), nameof(Targeter.TargeterUpdate))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Targeter_TargeterUpdate
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Targeter), "CurrentTargetUnderMouse")))
      .NonFocusedMapVehicle(out var vehicle,
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Targeter), nameof(Targeter.targetingSource)),
        AccessTools.PropertyGetter(typeof(ITargetingSource), nameof(ITargetingSource.Caster)).CallvirtInstruction)
      .FocusVehicleAroundMethod(vehicle,
        AccessTools.Method(typeof(ITargetingSource), nameof(ITargetingSource.DrawHighlight)))
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Targeter), "highlightAction")))
      .FocusVehicleAroundMethod(vehicle, AccessTools.Method(typeof(Action<LocalTargetInfo>), nameof(Action.Invoke)))
      .InstructionEnumeration();
  }
}