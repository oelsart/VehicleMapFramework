using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(AttackTargetsCache), nameof(AttackTargetsCache.UpdateTarget))]
    public static class Patch_AttackTargetsCache_UpdateTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(AttackTargetsCache), "RegisterTarget")]
    public static class Patch_AttackTargetsCache_RegisterTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing);
        }
    }

    [StaticConstructorOnStartup]
    public static class Patch_AttackTargetFinder
    {
        static Patch_AttackTargetFinder()
        {
            var transpiler = AccessTools.Method(typeof(Patch_AttackTargetFinder), nameof(Patch_AttackTargetFinder.Transpiler));

            foreach(var method in typeof(AttackTargetFinder).GetDeclaredMethods())
            {
                Core.harmonyInstance.Patch(method, null, null, transpiler);
            }

            foreach(var innerType in typeof(AttackTargetFinder).GetNestedTypes(AccessTools.all))
            {
                foreach(var method in innerType.GetDeclaredMethods())
                {
                    Core.harmonyInstance.Patch(method, null, null, transpiler);
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing)
                .MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap)
                .MethodReplacer(VehicleMapUtility.m_TargetInfo_Cell, VehicleMapUtility.m_CellOnBaseMap)
                .MethodReplacer(VehicleMapUtility.m_OccupiedRect, VehicleMapUtility.m_MovedOccupiedRect);
        }
    }

    [HarmonyPatch(typeof(PawnLeaner), nameof(PawnLeaner.Notify_WarmingCastAlongLine))]
    public static class Patch_PawnLeaner_Notify_WarmingCastAlongLine
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef))]
    public static class Patch_Projectile_Launch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_TargetInfo_Cell, VehicleMapUtility.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Projectile), "CanHit")]
    public static class Patch_Projectile_CanHit
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(ShootLeanUtility), nameof(ShootLeanUtility.CalcShootableCellsOf))]
    public static class Patch_ShootLeanUtility_CalcShootableCellsOf
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing)
                .MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(GenSight), nameof(GenSight.LineOfSightToThing))]
    public static class Patch_GenSight_LineOfSightToThing
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap)
                .MethodReplacer(VehicleMapUtility.m_OccupiedRect, VehicleMapUtility.m_MovedOccupiedRect);
        }
    }

    [HarmonyPatch(typeof(GenClosest), "<ClosestThing_Global_NewTemp>g__Process|9_0")]
    public static class Patch_GenClosest_ClosestThing_Global_NewTemp
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.PositionHeld)), VehicleMapUtility.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class Patch_ShotReport_HitReportFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing)
                .MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap)
                .MethodReplacer(VehicleMapUtility.m_TargetInfo_Cell, VehicleMapUtility.m_CellOnBaseMap)
                .MethodReplacer(VehicleMapUtility.m_ToTargetInfo, VehicleMapUtility.m_ToBaseMapTargetInfo);
        }
    }

    [HarmonyPatch(typeof(VerbUtility), nameof(VerbUtility.ThingsToHit))]
    public static class Patch_VerbUtility_ThingsToHit
    {
        public static void Postfix(IntVec3 cell, Map map, Func<Thing, bool> filter, List<Thing> __result)
        {
            if (!cell.InBounds(map)) return;
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
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing)
                .MethodReplacer(VehicleMapUtility.m_ToTargetInfo, VehicleMapUtility.m_ToBaseMapTargetInfo);
        }
    }

    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
    public static class Patch_Stance_Warmup_StanceTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(VehicleMapUtility.m_Thing_Map, VehicleMapUtility.m_BaseMapOfThing)
                .MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap);
        }
    }
}
