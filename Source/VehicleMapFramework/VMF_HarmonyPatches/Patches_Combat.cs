using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
[PatchLevel(Level.Safe)]
public static class Patch_AttackTargetFinder_BestAttackTarget
{
  public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange,
    bool canBashFences, bool onlyRanged, ref IAttackTarget __result)
  {
    if (!searcher.Thing.Map.CrossMapContext) return;
    var target = AttackTargetFinderOnVehicle.BestAttackTarget(searcher, flags, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canBashDoors, canTakeTargetsCloserThanEffectiveMinRange, canBashFences, onlyRanged);
    __result = AttackTargetFinderOnVehicle.CompareTarget(__result, target, searcher);
  }
}

[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanSee))]
[PatchLevel(Level.Safe)]
public static class Patch_AttackTargetFinder_CanSee
{
  public static bool Prefix(Thing seer, Thing target, Func<IntVec3, bool> validator, ref bool __result)
  {
    if (seer.Map != target.Map && seer.BaseMap() == target.BaseMap())
    {
      __result = seer.CanSee(target, validator);
      return false;
    }
    return true;
  }
}

[HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.Notify_WarmingCastAlongLine))]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnLeaner_Notify_WarmingCastAlongLine
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.LeanOffset), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_PawnLeaner_LeanOffset
{
  public static void Postfix(Pawn ___pawn, ref Vector3 __result)
  {
    if (___pawn.IsOnVehicleMapOf(out var vehicle))
    {
      __result = __result.RotatedBy(-vehicle.FullAngle);
    }
  }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef))]
[PatchLevel(Level.Cautious)]
public static class Patch_Projectile_Launch
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(1),
        CodeInstruction.LoadArgument(7))
      .SetInstruction(((Delegate)TargetCell).Method.CallInstruction)
      .InstructionEnumeration();
  }

  // ManTurretJobのpawnのターゲットマップではなくタレット自体のターゲットマップでベースマップ座標を取得する
  private static IntVec3 TargetCell(ref LocalTargetInfo targ, Thing launcher, Thing equipment)
  {
    if (launcher.IsOnNonFocusedVehicleMapOf(out var vehicle) && !vehicle.Spawned)
      return targ.Cell;

    var thing = launcher;
    if (equipment is not null && equipment.TryGetComp<CompMannable>(out var comp) && comp.ManningPawn == launcher)
    {
      thing = equipment;
    }

    return targ.TargetCellOnBaseMap(thing);
  }
}

//最初のthing.MapをBaseMapに変更し、ThingCoveredには逆にthing.Mapを渡す
[HarmonyPatch(typeof(Projectile), "CanHit")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Projectile_CanHit
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing)
      .MatchStartForward(
        new CodeMatch(OpCodes.Ldarg_0),
        CodeMatch.Calls(CachedMethodInfo.g_Thing_Map),
        CodeMatch.Calls(((Delegate)CoverUtility.ThingCovered).Method))
      .SetOpcodeAndAdvance(OpCodes.Ldarg_1)
      .SetOpcodeAndAdvance(OpCodes.Callvirt)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckIntercept))]
[PatchLevel(Level.Mandatory)]
public static class Patch_CompProjectileInterceptor_CheckIntercept
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool CheckIntercept(CompProjectileInterceptor instance, Projectile projectile,
    Vector3 lastExactPos, Vector3 newExactPos)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
  }
}

[HarmonyPatch(typeof(Projectile), "CheckForFreeInterceptBetween")]
[PatchLevel(Level.Sensitive)]
[StaticConstructorOnStartup]
public static class Patch_Projectile_CheckForFreeInterceptBetween
{
  // TabulaRasaのPostfixの引数順にちなむ
  public delegate void Postfix(Projectile __instance, ref bool __result, Vector3 lastExactPos, Vector3 newExactPos);

  public delegate bool Prefix(Projectile __instance, Vector3 lastExactPos, Vector3 newExactPos, ref bool __result);

  private static readonly Action<Projectile, Thing, bool> Impact =
    AccessTools.MethodDelegate<Action<Projectile, Thing, bool>>(AccessTools.Method(typeof(Projectile), "Impact"));

  public static List<Prefix> Prefixes { get; } = [];

  public static List<Postfix> Postfixes { get; } = [VanillaIntercept];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(new CodeMatch(OpCodes.Ldarg_1), CodeMatch.Calls(CachedMethodInfo.m_ToIntVec3))
      .CreateLabel(out var label)
      .Insert(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadArgument(1),
        CodeInstruction.LoadArgument(2),
        ((Delegate)CheckInterceptCrossMap).Method.CallInstruction,
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldc_I4_1),
        new CodeInstruction(OpCodes.Ret))
      .InstructionEnumeration();
  }

  private static bool CheckInterceptCrossMap(Projectile instance, Vector3 lastExactPos, Vector3 newExactPos)
  {
    if (!instance.Spawned) return false;

    foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(instance.Map))
    {
      instance.TargetMap = vehicle.VehicleMap;
      try
      {
        for (var i = 0; i < Prefixes.Count; i++)
        {
          var result = false;
          if (!Prefixes[i](instance, lastExactPos, newExactPos, ref result) && result) return true;
        }

        for (var i = 0; i < Postfixes.Count; i++)
        {
          var result = false;
          Postfixes[i](instance, ref result, lastExactPos, newExactPos);
          if (result)
          {
            Impact(instance, null, true);
            return true;
          }
        }
      }
      finally
      {
        instance.RemoveTargetInfo();
      }
    }
    return false;
  }

  private static void VanillaIntercept(Projectile instance, ref bool __result, Vector3 lastExactPos, Vector3 newExactPos)
  {
    var list = instance.TargetMapOrThingMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
    for (var i = 0; i < list.Count; i++)
    {
      if (Patch_CompProjectileInterceptor_CheckIntercept.CheckIntercept(
            list[i].TryGetComp<CompProjectileInterceptor>(),
            instance,
            lastExactPos,
            newExactPos))
      {
        __result = true;
        return;
      }
    }
  }
}

[HarmonyPatch(typeof(Projectile), "CheckForFreeIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_Projectile_CheckForFreeIntercept
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
  }
}

[HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckBombardmentIntercept))]
[PatchLevel(Level.Mandatory)]
public static class Patch_CompProjectileInterceptor_CheckBombardmentIntercept
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool CheckBombardmentIntercept(CompProjectileInterceptor instance, Bombardment bombardment,
    Bombardment.BombardmentProjectile projectile)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
  }
}

[HarmonyPatch(typeof(Bombardment), "TryDoExplosion")]
[PatchLevel(Level.Safe)]
public static class Patch_Bombardment_TryDoExplosion
{
  public static List<Func<Bombardment, Bombardment.BombardmentProjectile, bool>> Prefixes { get; } = [VanillaIntercept];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(
        new CodeMatch(OpCodes.Ldarg_1),
        CodeMatch.LoadsField(
          AccessTools.Field(typeof(Bombardment.BombardmentProjectile),
            nameof(Bombardment.BombardmentProjectile.targetCell))))
      .CreateLabel(out var label)
      .Insert(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadArgument(1),
        ((Delegate)CheckInterceptCrossMap).Method.CallInstruction,
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ret))
      .InstructionEnumeration();
  }

  private static bool CheckInterceptCrossMap(Bombardment __instance, Bombardment.BombardmentProjectile proj)
  {
    if (!__instance.Spawned) return false;

    foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(__instance.Map))
    {
      using var _ = new VirtualTeleporter(__instance, vehicle.VehicleMap);
      for (var i = 0; i < Prefixes.Count; i++)
      {
        if (!Prefixes[i](__instance, proj)) return true;
      }
    }
    return false;
  }

  private static bool VanillaIntercept(Bombardment __instance, Bombardment.BombardmentProjectile proj)
  {
    var list = __instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
    for (var i = 0; i < list.Count; i++)
    {
      if (Patch_CompProjectileInterceptor_CheckBombardmentIntercept.CheckBombardmentIntercept(
            list[i].TryGetComp<CompProjectileInterceptor>(),
            __instance,
            proj))
      {
        return false;
      }
    }
    return true;
  }
}

//変更点はShotReportOnVehicle.HitReportForを参照のこと。このTranspilerは元メソッドをOnVehicleに変換するもの
[HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
[PatchLevel(Level.Sensitive)]
public static class Patch_ShotReport_HitReportFor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var codes = instructions.ToList();
    var targThing = generator.DeclareLocal(typeof(Thing));
    var targetMap = generator.DeclareLocal(typeof(Map));
    var casterPositionOnTargetMap = generator.DeclareLocal(typeof(IntVec3));

    //冒頭のtargetMapとcasterPositionOnTargetMapの計算
    var label = generator.DefineLabel();
    var label2 = generator.DefineLabel();
    var label3 = generator.DefineLabel();
    var label4 = generator.DefineLabel();
    codes.InsertRange(0,
    [
      CodeInstruction.LoadArgument(2, true),
      AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Thing)).CallInstruction,
      new CodeInstruction(OpCodes.Stloc_S, targThing),
      new CodeInstruction(OpCodes.Ldloc_S, targThing),
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Ldloc_S, targThing),
      CachedMethodInfo.g_Thing_Map.CallvirtInstruction,
      new CodeInstruction(OpCodes.Br_S, label2),
      CodeInstruction.LoadArgument(0).WithLabels(label),
      CachedMethodInfo.m_BaseMap_Thing.CallInstruction,
      new CodeInstruction(OpCodes.Stloc_S, targetMap).WithLabels(label2),
      new CodeInstruction(OpCodes.Ldloc_S, targThing),
      new CodeInstruction(OpCodes.Brfalse_S, label3),
      CodeInstruction.LoadArgument(0),
      new CodeInstruction(OpCodes.Ldloc_S, targThing),
      CachedMethodInfo.m_PositionOnAnotherThingMap.CallInstruction,
      new CodeInstruction(OpCodes.Br_S, label4),
      CodeInstruction.LoadArgument(0).WithLabels(label3),
      CachedMethodInfo.m_PositionOnBaseMap.CallInstruction,
      new CodeInstruction(OpCodes.Stloc_S, casterPositionOnTargetMap).WithLabels(label4)
    ]);

    var pos2 = 0;
    for (var i = 0; i < 3; i++)
    {
      //caster.Position -> casterPositionOnTargetMap
      var pos = codes.FindIndex(pos2, c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Position));
      codes[pos].opcode = OpCodes.Ldloc_S;
      codes[pos].operand = casterPositionOnTargetMap;
      codes.RemoveAt(pos - 1);

      //caster.Map -> targetMap
      pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
      codes[pos2].opcode = OpCodes.Ldloc_S;
      codes[pos2].operand = targetMap;
      codes.RemoveAt(pos2 - 1);
    }

    var codes1 = codes.Take(pos2);
    var codes2 = codes.Skip(pos2).MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));

    return codes1.Concat(codes2);
  }
}

[HarmonyPatch(typeof(VerbUtility), nameof(VerbUtility.ThingsToHit))]
[PatchLevel(Level.Cautious)]
public static class Patch_VerbUtility_ThingsToHit
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
  }
}

[HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.InitEffects))]
[PatchLevel(Level.Cautious)]
public static class Patch_Stance_Warmup_InitEffects
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_ToTargetInfo, CachedMethodInfo.m_ToBaseMapTargetInfo));
  }
}

[HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
[PatchLevel(Level.Cautious)]
public static class Patch_Stance_Warmup_StanceTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
[PatchLevel(Level.Cautious)]
public static class Patch_Pawn_TryStartAttack
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(Building_Turret), "Tick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_Turret_Tick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMapOrCaravan_Thing);
  }
}

[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGun_TryFindNewTarget_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(typeof(Building_TurretGun),
      t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatch(typeof(Building_TurretFoam), nameof(Building_TurretFoam.TryFindNewTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretFoam_TryFindNewTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2),
      (CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps));
  }
}

[HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.OrderAttack))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_Turret_OrderAttack
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    instructions = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    foreach (var instruction in instructions)
    {
      if (instruction.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CachedMethodInfo.m_TargetCellOnBaseMap.CallInstruction;
      }
      else
      {
        yield return instruction;
      }
    }
  }
}

[HarmonyPatch(typeof(Building_TurretGun), "IsValidTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGun_IsValidTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }
}

[HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.DrawExtraSelectionOverlays))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGun_DrawExtraSelectionOverlays
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      if (instruction.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CachedMethodInfo.m_TargetCellOnBaseMap.CallInstruction;
      }
      else
      {
        yield return instruction;
      }
    }
  }
}

[HarmonyPatch(typeof(TurretTop), nameof(TurretTop.TurretTopTick))]
[PatchLevel(Level.Sensitive)]
public static class Patch_TurretTop_TurretTopTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      if (instruction.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CodeInstruction.LoadField(typeof(TurretTop), "parentTurret");
        yield return CachedMethodInfo.m_TargetCellOnBaseMap.CallInstruction;
      }
      else
      {
        yield return instruction;
      }
    }
  }
}

//Turretがターゲットに向いていない時タレットの見た目上の回転に車の回転を加える。無きゃないでいい
[HarmonyPatch(typeof(TurretTop), nameof(TurretTop.DrawTurret))]
[PatchLevel(Level.Sensitive)]
public static class Patch_TurretTop_DrawTurret
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_RotatedBy))
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .CreateLabel(out var label)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(TurretTop), "parentTurret"),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf.CallInstruction,
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        CachedMethodInfo.g_Angle.CallvirtInstruction,
        new CodeInstruction(OpCodes.Sub))
      .MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalType == typeof(Quaternion)))
      .CreateLabel(out var label2)
      .DeclareLocal(typeof(LocalTargetInfo), out var target)
      .Insert(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Brfalse_S, label2),
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(TurretTop), "parentTurret"),
        AccessTools.PropertyGetter(typeof(Building_Turret), nameof(Building_Turret.CurrentTarget)).CallvirtInstruction,
        new CodeInstruction(OpCodes.Stloc_S, target),
        new CodeInstruction(OpCodes.Ldloca_S, target),
        AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.IsValid)).CallInstruction,
        new CodeInstruction(OpCodes.Brtrue_S, label2),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        CachedMethodInfo.m_FullAngleQuat.CallInstruction,
        CachedMethodInfo.o_Quaternion_Multiply.CallInstruction)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(DamageWorker), nameof(DamageWorker.ExplosionCellsToHit), typeof(IntVec3), typeof(Map), typeof(float), typeof(IntVec3?), typeof(IntVec3?), typeof(FloatRange?))]
[PatchLevel(Level.Cautious)]
public static class Patch_DamageWorker_ExplosionCellsToHit
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1),
      (CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2));
  }
}

[HarmonyPatch(typeof(Explosion), "AffectCell")]
public static class Patch_Explosion_AffectCell
{
  [HarmonyPriority(Priority.Normal)]
  [PatchLevel(Level.Safe)]
  public static void Postfix(Explosion __instance, IntVec3 c)
  {
    if (c.TryGetVehicleMap(__instance.Map, out var vehicle))
    {
      var c2 = c.ToVehicleMapCoord(vehicle);
      if (!c2.InBounds(vehicle.VehicleMap)) return;
      using var _ = new VirtualTeleporter(__instance, vehicle.VehicleMap, __instance.Position.ToVehicleMapCoord(vehicle));
      AffectCell(__instance, c2, c);
    }
  }

  [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
  [HarmonyPriority(Priority.HigherThanNormal)]
  [PatchLevel(Level.Mandatory)]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static void AffectCell(Explosion instance, IntVec3 c2, IntVec3 c)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return new CodeMatcher(instructions)
        .MatchStartForward(new CodeMatch(OpCodes.Ldarg_1),
          CodeMatch.Calls(AccessTools.Method(typeof(Explosion), "ShouldCellBeAffectedOnlyByDamage")))
        .Repeat(matcher => matcher.SetOpcodeAndAdvance(OpCodes.Ldarg_2))
        .InstructionEnumeration()
        .MethodReplacer(
          (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnTargetMap),
          (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap));
    }
  }
}

[HarmonyPatch(typeof(Projectile_Liquid), "DoImpact")]
[PatchLevel(Level.Safe)]
public static class Patch_Projectile_Liquid_DoImpact
{
  public static bool Prefix(Projectile_Liquid __instance, Thing hitThing, IntVec3 cell, ThingDef ___targetCoverDef)
  {
    if (cell.TryGetVehicleMap(__instance.Map, out var vehicle))
    {
      var cell2 = cell.ToVehicleMapCoord(vehicle);
      if (__instance.def.projectile.filth != null && __instance.def.projectile.filthCount.TrueMax > 0 && !cell2.Filled(vehicle.VehicleMap))
      {
        FilthMaker.TryMakeFilth(cell2, vehicle.VehicleMap, __instance.def.projectile.filth, __instance.def.projectile.filthCount.RandomInRange);
      }
      var thingList = cell2.GetThingList(vehicle.VehicleMap);
      for (var i = 0; i < thingList.Count; i++)
      {
        var thing = thingList[i];
        if (thing is not Mote && thing is not Filth && thing != hitThing)
        {
          Find.BattleLog.Add(new BattleLogEntry_RangedImpact(__instance.Launcher, thing, thing, __instance.EquipmentDef, __instance.def, ___targetCoverDef));
          DamageInfo dinfo = new(__instance.def.projectile.damageDef, __instance.def.projectile.GetDamageAmount(null), __instance.def.projectile.GetArmorPenetration(), -1f, __instance.Launcher);
          thing.TakeDamage(dinfo);
        }
      }
      return false;
    }
    return true;
  }
}

[HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.Roofed), typeof(IntVec3))]
[PatchLevel(Level.Safe)]
public static class Patch_RoofGrid_Roofed
{
  private static bool Prepare()
  {
    return VehicleMapFramework.settings is { roofedPatch: true };
  }

  public static void Postfix(IntVec3 c, Map ___map, ref bool __result)
  {
    if (___map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
    {
      IntVec3 c2;
      __result = __result || (c2 = c.ToBaseMapCoord(vehicle)).InBounds(vehicle.Map) && vehicle.Map.roofGrid.RoofAt(c2) != null;
    }
  }
}

[HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
[PatchLevel(Level.Sensitive)]
public static class Patch_JobGiver_AIFightEnemy_TryGiveJob
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var g_LengthHorizontalSquared = AccessTools.PropertyGetter(typeof(IntVec3), nameof(IntVec3.LengthHorizontalSquared));
    var pos = codes.FindIndex(c => c.Calls(g_LengthHorizontalSquared));

    for (var i = 0; i < 2; i++)
    {
      pos = codes.FindLastIndex(pos - 1, c => c.Calls(CachedMethodInfo.g_Thing_Position));
      codes[pos].opcode = OpCodes.Call;
      codes[pos].operand = CachedMethodInfo.m_PositionOnBaseMap;
    }
    return codes;
  }
}

[HarmonyPatch(typeof(JobGiver_AIFightEnemy), "UpdateEnemyTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_JobGiver_AIFightEnemy_UpdateEnemyTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatch(typeof(JobGiver_AIFightEnemy), "ShouldLoseTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_JobGiver_AIFightEnemy_ShouldLoseTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatch(typeof(CastPositionFinder), nameof(CastPositionFinder.TryFindCastPosition))]
[PatchLevel(Level.Safe)]
public static class Patch_CastPositionFinder_TryFindCastPosition
{
  public static bool Prefix(CastPositionRequest newReq, ref IntVec3 dest, ref bool __result)
  {
    if (newReq.caster.Map != newReq.target.MapHeld && newReq.caster.BaseMap() == newReq.target.MapHeldBaseMap())
    {
      __result = CastPositionFinderOnVehicle.TryFindCastPosition(newReq, out dest);
      return false;
    }
    return true;
  }
}
