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
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(AttackTargetsCache), "RegisterTarget")]
    public static class Patch_AttackTargetsCache_RegisterTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
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
            return instructions.MethodReplacer(MethodInfoCache.g_TargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
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
            codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMapOfThing);

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
            var result = new List<Thing>();
            MapParent_Vehicle parentVehicle1 = null;
            if ((parentVehicle1 = launcher.Map.Parent as MapParent_Vehicle) != null)
            {
                result.AddRange(parentVehicle1.vehicle.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor));
            }
            MapParent_Vehicle parentVehicle2 = null;
            if (instance.usedTarget.HasThing && (parentVehicle2 = instance.usedTarget.Thing.Map.Parent as MapParent_Vehicle) != null)
            {
                result.AddRange(parentVehicle2.vehicle.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor));
            }
            if (parentVehicle1 == null && parentVehicle2 == null)
            {
                result.AddRange(list);
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
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing)
                .MethodReplacer(MethodInfoCache.m_ToTargetInfo, MethodInfoCache.m_ToBaseMapTargetInfo);
        }
    }

    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
    public static class Patch_Stance_Warmup_StanceTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
    public static class Patch_Pawn_TryStartAttack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_TargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
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
}
