using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
    public static class Patch_AttackTargetFinder_BestAttackTarget
    {
        public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canBashDoors, bool canTakeTargetsCloserThanEffectiveMinRange, bool canBashFences, bool onlyRanged, ref IAttackTarget __result)
        {
            if (Find.TickManager.TicksGame - lastSearchTick > 10)
            {
                var target = AttackTargetFinderOnVehicle.BestAttackTarget(searcher, flags, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canBashDoors, canTakeTargetsCloserThanEffectiveMinRange, canBashFences, onlyRanged);
                if (__result == null || target != null && (__result.Thing.Position - searcher.Thing.Position).LengthHorizontalSquared > (target.Thing.PositionOnBaseMap() - searcher.Thing.PositionOnBaseMap()).LengthHorizontalSquared)
                {
                    __result = target;
                }
                lastSearchTick = Find.TickManager.TicksGame;
            }
        }

        private static int lastSearchTick = -1;
    }

    [HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.Notify_WarmingCastAlongLine))]
    public static class Patch_PawnLeaner_Notify_WarmingCastAlongLine
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.LeanOffset), MethodType.Getter)]
    public static class Patch_PawnLeaner_LeanOffset
    {
        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (___pawn.IsOnVehicleMapOf(out var vehicle))
            {
                __result = __result.RotatedBy(-vehicle.FullRotation.AsAngle);
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef))]
    public static class Patch_Projectile_Launch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    //最初のthing.MapをBaseMapに変更し、ThingCoveredには逆にthing.Mapを渡す
    [HarmonyPatch(typeof(Projectile), "CanHit")]
    public static class Patch_Projectile_CanHit
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing);

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
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);

            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(Projectile), "launcher"),
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_Projectile_CheckForFreeInterceptBetween), nameof(Patch_Projectile_CheckForFreeInterceptBetween.IncludeVehicleMapIntercepters))
            });
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

        private static readonly List<Thing> tmpList = new List<Thing>();
    }

    [HarmonyPatch(typeof(Projectile), "CheckForFreeIntercept")]
    public static class Patch_Projectile_CheckForFreeIntercept
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_2);

            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(Projectile), "launcher"),
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_Projectile_CheckForFreeInterceptBetween), nameof(Patch_Projectile_CheckForFreeInterceptBetween.IncludeVehicleMapIntercepters))
            });
            return codes;
        }
    }

    //変更点はShotReportOnVehicle.HitReportForを参照のこと。このTranspilerは元メソッドをOnVehicleに変換するもの
    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class Patch_ShotReport_HitReportFor
    {
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
            codes.InsertRange(0, new[]
            {
                CodeInstruction.LoadArgument(2, true),
                new CodeInstruction(OpCodes.Call, g_Thing),
                new CodeInstruction(OpCodes.Stloc_S, targThing),
                new CodeInstruction(OpCodes.Ldloc_S, targThing),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, targThing),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Map),
                new CodeInstruction(OpCodes.Br_S, label2),
                CodeInstruction.LoadArgument(0).WithLabels(label),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing),
                new CodeInstruction(OpCodes.Stloc_S, targetMap).WithLabels(label2),
                new CodeInstruction(OpCodes.Ldloc_S, targThing),
                new CodeInstruction(OpCodes.Brfalse_S, label3),
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloc_S, targThing),
                new CodeInstruction(OpCodes.Call, m_PositionOnAnotherThingMap),
                new CodeInstruction(OpCodes.Br_S, label4),
                CodeInstruction.LoadArgument(0).WithLabels(label3),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_PositionOnBaseMap),
                new CodeInstruction(OpCodes.Stloc_S, casterPositionOnTargetMap).WithLabels(label4),
            });

            var pos2 = 0;
            for (var i = 0; i < 3; i++)
            {
                //caster.Position -> casterPositionOnTargetMap
                var pos = codes.FindIndex(pos2, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
                codes[pos].opcode = OpCodes.Ldloc_S;
                codes[pos].operand = casterPositionOnTargetMap;
                codes.RemoveAt(pos - 1);

                //caster.Map -> targetMap
                pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
                codes[pos2].opcode = OpCodes.Ldloc_S;
                codes[pos2].operand = targetMap;
                codes.RemoveAt(pos2 - 1);
            }

            var codes1 = codes.Take(pos2);
            var codes2 = codes.Skip(pos2).MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle);

            return codes1.Concat(codes2);
        }
    }

    [HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckIntercept))]
    public static class Patch_CompProjectileInterceptor_CheckIntercept
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(VerbUtility), nameof(VerbUtility.ThingsToHit))]
    public static class Patch_VerbUtility_ThingsToHit
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.InitEffects))]
    public static class Patch_Stance_Warmup_InitEffects
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_ToTargetInfo, MethodInfoCache.m_ToBaseMapTargetInfo);
        }
    }

    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
    public static class Patch_Stance_Warmup_StanceTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
    public static class Patch_Pawn_TryStartAttack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.TryFindNewTarget))]
    public static class Patch_Building_Turret_TryFindNewTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Building_TurretFoam), nameof(Building_TurretFoam.TryFindNewTarget))]
    public static class Patch_Building_TurretFoam_TryFindNewTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight2, MethodInfoCache.m_GenSightOnVehicle_LineOfSight2)
                .MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.OrderAttack))]
    public static class Patch_Building_Turret_OrderAttack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(TurretTop), nameof(TurretTop.TurretTopTick))]
    public static class Patch_TurretTop_TurretTopTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    //Turretがターゲットに向いていない時タレットの見た目上の回転に車の回転を加える。無きゃないでいい
    [HarmonyPatch(typeof(TurretTop), nameof(TurretTop.DrawTurret))]
    public static class Patch_TurretTop_DrawTurret
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 5);
            var label = generator.DefineLabel();
            var target = generator.DeclareLocal(typeof(LocalTargetInfo));
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(TurretTop), "parentTurret"),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(TurretTop), "parentTurret"),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Building_Turret), nameof(Building_Turret.CurrentTarget))),
                new CodeInstruction(OpCodes.Stloc_S, target),
                new CodeInstruction(OpCodes.Ldloca_S, target),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.IsValid))),
                new CodeInstruction(OpCodes.Brtrue_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_Rot8_AsQuat),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.o_Quaternion_Multiply),
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(DamageWorker), nameof(DamageWorker.ExplosionCellsToHit), typeof(IntVec3), typeof(Map), typeof(float), typeof(IntVec3?), typeof(IntVec3?), typeof(FloatRange?))]
    public static class Patch_DamageWorker_ExplosionCellsToHit
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight1, MethodInfoCache.m_GenSightOnVehicle_LineOfSight1)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight2, MethodInfoCache.m_GenSightOnVehicle_LineOfSight2);
        }
    }

    [HarmonyPatch(typeof(Projectile_Liquid), "DoImpact")]
    public static class Patch_Projectile_Liquid_DoImpact
    {
        public static bool Prefix(Projectile_Liquid __instance, Thing hitThing, IntVec3 cell, ThingDef ___targetCoverDef)
        {
            if (cell.ToVector3Shifted().TryGetVehicleMap(__instance.Map, out var vehicle))
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
                    if (!(thing is Mote) && !(thing is Filth) && thing != hitThing)
                    {
                        Find.BattleLog.Add(new BattleLogEntry_RangedImpact(__instance.Launcher, thing, thing, __instance.EquipmentDef, __instance.def, ___targetCoverDef));
                        DamageInfo dinfo = new DamageInfo(__instance.def.projectile.damageDef, (float)__instance.def.projectile.GetDamageAmount(null, null), __instance.def.projectile.GetArmorPenetration(null, null), -1f, __instance.Launcher, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true);
                        thing.TakeDamage(dinfo);
                    }
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.Roofed), new Type[] { typeof(IntVec3) })]
    public static class Patch_RoofGrid_Roofed
    {
        private static bool Prepare()
        {
            return VehicleInteriors.settings.roofedPatch;
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
}