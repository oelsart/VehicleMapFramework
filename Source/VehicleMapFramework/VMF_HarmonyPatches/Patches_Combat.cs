using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
public static class Patch_AttackTargetFinder_BestAttackTarget
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange, bool canBashFences, bool onlyRanged, ref IAttackTarget __result)
    {
        var map = searcher.Thing.Map;
        if ((searcher is Building_Turret && !searcher.Thing.IsHashIntervalTick(10)) || !map.BaseMapAndVehicleMaps().Except(map).Any()) return;

        var target = AttackTargetFinderOnVehicle.BestAttackTarget(searcher, flags, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canBashDoors, canTakeTargetsCloserThanEffectiveMinRange, canBashFences, onlyRanged);
        if (__result == null || (target != null && (__result.Thing.Position - searcher.Thing.Position).LengthHorizontalSquared > (target.Thing.PositionOnBaseMap() - searcher.Thing.PositionOnBaseMap()).LengthHorizontalSquared))
        {
            __result = target;
        }
    }
}

[HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.Notify_WarmingCastAlongLine))]
public static class Patch_PawnLeaner_Notify_WarmingCastAlongLine
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.LeanOffset), MethodType.Getter)]
public static class Patch_PawnLeaner_LeanOffset
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(Pawn ___pawn, ref Vector3 __result)
    {
        if (___pawn.IsOnVehicleMapOf(out var vehicle))
        {
            __result = __result.RotatedBy(-vehicle.FullAngle());
        }
    }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef))]
public static class Patch_Projectile_Launch
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

//最初のthing.MapをBaseMapに変更し、ThingCoveredには逆にthing.Mapを渡す
[HarmonyPatch(typeof(Projectile), "CanHit")]
public static class Patch_Projectile_CanHit
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
        codes[pos] = new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing);

        var m_ThingCovered = AccessTools.Method(typeof(CoverUtility), nameof(CoverUtility.ThingCovered));
        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(m_ThingCovered)) - 2;
        codes[pos2] = CodeInstruction.LoadArgument(1);
        codes[pos2 + 1].opcode = OpCodes.Callvirt;
        return codes;
    }
}

[HarmonyPatch(typeof(Projectile), "CheckForFreeInterceptBetween")]
public static class Patch_Projectile_CheckForFreeInterceptBetween
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);

        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(Projectile), "launcher"),
            CodeInstruction.LoadArgument(0),
            CodeInstruction.Call(typeof(Patch_Projectile_CheckForFreeInterceptBetween), nameof(IncludeVehicleMapIntercepters))
        ]);
        return codes;
    }

    public static List<Thing> IncludeVehicleMapIntercepters(List<Thing> list, Thing launcher, Projectile instance)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        if (launcher.IsOnVehicleMapOf(out var vehicle))
        {
            tmpList.AddRange(vehicle.VehicleMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor));
        }
        if (instance.usedTarget.HasThing && instance.usedTarget.Thing.IsOnVehicleMapOf(out var vehicle2))
        {
            tmpList.AddRange(vehicle2.VehicleMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor));
        }
        return tmpList;
    }

    private static readonly List<Thing> tmpList = [];
}

[HarmonyPatch(typeof(Projectile), "CheckForFreeIntercept")]
public static class Patch_Projectile_CheckForFreeIntercept
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_2);

        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(Projectile), "launcher"),
            CodeInstruction.LoadArgument(0),
            CodeInstruction.Call(typeof(Patch_Projectile_CheckForFreeInterceptBetween), nameof(Patch_Projectile_CheckForFreeInterceptBetween.IncludeVehicleMapIntercepters))
        ]);
        return codes;
    }
}

//変更点はShotReportOnVehicle.HitReportForを参照のこと。このTranspilerは元メソッドをOnVehicleに変換するもの
[HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
public static class Patch_ShotReport_HitReportFor
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var targThing = generator.DeclareLocal(typeof(Thing));
        var targetMap = generator.DeclareLocal(typeof(Map));
        var casterPositionOnTargetMap = generator.DeclareLocal(typeof(IntVec3));

        //冒頭のtargetMapとcasterPositionOnTargetMapの計算
        var g_Thing = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Thing));
        var m_PositionOnAnotherThingMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.PositionOnAnotherThingMap));
        var label = generator.DefineLabel();
        var label2 = generator.DefineLabel();
        var label3 = generator.DefineLabel();
        var label4 = generator.DefineLabel();
        codes.InsertRange(0,
        [
            CodeInstruction.LoadArgument(2, true),
            new CodeInstruction(OpCodes.Call, g_Thing),
            new CodeInstruction(OpCodes.Stloc_S, targThing),
            new CodeInstruction(OpCodes.Ldloc_S, targThing),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, targThing),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
            new CodeInstruction(OpCodes.Br_S, label2),
            CodeInstruction.LoadArgument(0).WithLabels(label),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing),
            new CodeInstruction(OpCodes.Stloc_S, targetMap).WithLabels(label2),
            new CodeInstruction(OpCodes.Ldloc_S, targThing),
            new CodeInstruction(OpCodes.Brfalse_S, label3),
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Ldloc_S, targThing),
            new CodeInstruction(OpCodes.Call, m_PositionOnAnotherThingMap),
            new CodeInstruction(OpCodes.Br_S, label4),
            CodeInstruction.LoadArgument(0).WithLabels(label3),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMap),
            new CodeInstruction(OpCodes.Stloc_S, casterPositionOnTargetMap).WithLabels(label4),
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
        var codes2 = codes.Skip(pos2).MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);

        return codes1.Concat(codes2);
    }
}

[HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckIntercept))]
public static class Patch_CompProjectileInterceptor_CheckIntercept
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(VerbUtility), nameof(VerbUtility.ThingsToHit))]
public static class Patch_VerbUtility_ThingsToHit
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
    }
}

[HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.InitEffects))]
public static class Patch_Stance_Warmup_InitEffects
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_ToTargetInfo, CachedMethodInfo.m_ToBaseMapTargetInfo);
    }
}

[HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
public static class Patch_Stance_Warmup_StanceTick
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
public static class Patch_Pawn_TryStartAttack
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(Building_Turret), "Tick")]
public static class Patch_Building_Turret_Tick
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.TryFindNewTarget))]
public static class Patch_Building_Turret_TryFindNewTarget
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Building_TurretFoam), nameof(Building_TurretFoam.TryFindNewTarget))]
public static class Patch_Building_TurretFoam_TryFindNewTarget
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2)
            .MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
    }
}

[HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.OrderAttack))]
public static class Patch_Building_Turret_OrderAttack
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        instructions = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetCellOnBaseMap);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

[HarmonyPatch(typeof(Building_TurretGun), "IsValidTarget")]
public static class Patch_Building_TurretGun_IsValidTarget
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.DrawExtraSelectionOverlays))]
public static class Patch_Building_TurretGun_DrawExtraSelectionOverlays
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetCellOnBaseMap);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

[HarmonyPatch(typeof(TurretTop), nameof(TurretTop.TurretTopTick))]
public static class Patch_TurretTop_TurretTopTick
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.LoadField(typeof(TurretTop), "parentTurret");
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_TargetCellOnBaseMap);
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
public static class Patch_TurretTop_DrawTurret
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0) - 1;
        var label = generator.DefineLabel();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var rot = generator.DeclareLocal(typeof(Rot8));
        codes[pos].labels.Add(label);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(TurretTop), "parentTurret"),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_FullRotation),
            new CodeInstruction(OpCodes.Stloc_S, rot),
            new CodeInstruction(OpCodes.Ldloca_S, rot),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Rot8_AsAngle),
            new CodeInstruction(OpCodes.Add)
        ]);

        pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 5);
        var label2 = generator.DefineLabel();
        var target = generator.DeclareLocal(typeof(LocalTargetInfo));

        codes[pos].labels.Add(label2);
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Brfalse_S, label2),
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(TurretTop), "parentTurret"),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Building_Turret), nameof(Building_Turret.CurrentTarget))),
            new CodeInstruction(OpCodes.Stloc_S, target),
            new CodeInstruction(OpCodes.Ldloca_S, target),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.IsValid))),
            new CodeInstruction(OpCodes.Brtrue_S, label2),
            new CodeInstruction(OpCodes.Ldloc_S, rot),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_Rot8_AsQuat),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply),
        ]);
        return codes;
    }
}

[HarmonyPatch(typeof(DamageWorker), nameof(DamageWorker.ExplosionCellsToHit), typeof(IntVec3), typeof(Map), typeof(float), typeof(IntVec3?), typeof(IntVec3?), typeof(FloatRange?))]
public static class Patch_DamageWorker_ExplosionCellsToHit
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2);
    }
}

[HarmonyPatch(typeof(Projectile_Liquid), "DoImpact")]
public static class Patch_Projectile_Liquid_DoImpact
{
    [PatchLevel(Level.Safe)]
    public static bool Prefix(Projectile_Liquid __instance, Thing hitThing, IntVec3 cell, ThingDef ___targetCoverDef)
    {
        if (cell.TryGetVehicleMap(__instance.Map, out var vehicle))
        {
            var cell2 = cell.ToVehicleMapCoord(vehicle);
            if (__instance.def.projectile.filth != null && __instance.def.projectile.filthCount.TrueMax > 0 && !cell2.Filled(vehicle.VehicleMap))
            {
                FilthMaker.TryMakeFilth(cell2, vehicle.VehicleMap, __instance.def.projectile.filth, __instance.def.projectile.filthCount.RandomInRange, FilthSourceFlags.None, true);
            }
            List<Thing> thingList = cell2.GetThingList(vehicle.VehicleMap);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                if (thing is not Mote && thing is not Filth && thing != hitThing)
                {
                    Find.BattleLog.Add(new BattleLogEntry_RangedImpact(__instance.Launcher, thing, thing, __instance.EquipmentDef, __instance.def, ___targetCoverDef));
                    DamageInfo dinfo = new(__instance.def.projectile.damageDef, __instance.def.projectile.GetDamageAmount(null, null), __instance.def.projectile.GetArmorPenetration(null, null), -1f, __instance.Launcher, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true);
                    thing.TakeDamage(dinfo);
                }
            }
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.Roofed), [typeof(IntVec3)])]
public static class Patch_RoofGrid_Roofed
{
    private static bool Prepare()
    {
        return VehicleMapFramework.settings.roofedPatch;
    }

    [PatchLevel(Level.Safe)]
    public static void Postfix(IntVec3 c, Map ___map, ref bool __result)
    {
        if (___map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            IntVec3 c2;
            __result = __result || ((c2 = c.ToBaseMapCoord(vehicle)).InBounds(vehicle.Map) && vehicle.Map.roofGrid.RoofAt(c2) != null);
        }
    }
}

[HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
public static class Patch_JobGiver_AIFightEnemy_TryGiveJob
{
    [PatchLevel(Level.Sensitive)]
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
public static class Patch_JobGiver_AIFightEnemy_UpdateEnemyTarget
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(JobGiver_AIFightEnemy), "ShouldLoseTarget")]
public static class Patch_JobGiver_AIFightEnemy_ShouldLoseTarget
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(CastPositionFinder), nameof(CastPositionFinder.TryFindCastPosition))]
public static class Patch_CastPositionFinder_TryFindCastPosition
{
    [PatchLevel(Level.Safe)]
    public static bool Prefix(Verse.AI.CastPositionRequest newReq, ref IntVec3 dest, ref bool __result)
    {
        if (newReq.caster.Map != newReq.target.MapHeld && newReq.caster.BaseMap() == newReq.target.MapHeldBaseMap())
        {
            __result = CastPositionFinderOnVehicle.TryFindCastPosition(newReq, out dest);
            return false;
        }
        return true;
    }
}