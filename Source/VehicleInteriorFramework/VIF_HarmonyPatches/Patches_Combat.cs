using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using VehicleInteriors.Jobs;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(AttackTargetsCache), nameof(AttackTargetsCache.UpdateTarget))]
    public static class Patch_AttackTargetsCache_UpdateTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(AttackTargetsCache), "RegisterTarget")]
    public static class Patch_AttackTargetsCache_RegisterTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
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
            var result = new List<Thing>(list);
            if (launcher.IsOnVehicleMapOf(out var vehicle))
            {
                result.AddRange(vehicle.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor));
            }
            if (instance.usedTarget.HasThing && instance.usedTarget.Thing.IsOnVehicleMapOf(out var vehicle2))
            {
                result.AddRange(vehicle2.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor));
            }
            return result;
        }
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
        public static void Postfix(IntVec3 cell, Map map, Func<Thing, bool> filter, List<Thing> __result)
        {
            foreach(var vehicle in cell.GetThingList(map).OfType<VehiclePawnWithInterior>())
            {
                var cellOnVehicle = cell.VehicleMapToOrig(vehicle);
                if (!cellOnVehicle.InBounds(vehicle.interiorMap)) return;
                foreach (var thing in cellOnVehicle.GetThingList(vehicle.interiorMap))
                {
                    if ((thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Pawn || thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Plant) && filter(thing))
                    {
                        __result.Add(thing);
                    }
                }
            }
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

    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class Patch_JobDriver_Wait_CheckForAutoAttack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestShootTargetFromCurrentPosition)),
                AccessTools.Method(typeof(AttackTargetFinderOnVehicle), nameof(AttackTargetFinderOnVehicle.BestShootTargetFromCurrentPosition)));
        }
    }

    [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.TryFindNewTarget))]
    public static class Patch_Building_Turret_TryFindNewTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(AccessTools.Method(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestShootTargetFromCurrentPosition)),
                AccessTools.Method(typeof(AttackTargetFinderOnVehicle), nameof(AttackTargetFinderOnVehicle.BestShootTargetFromCurrentPosition)));
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
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithInterior));

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
}
