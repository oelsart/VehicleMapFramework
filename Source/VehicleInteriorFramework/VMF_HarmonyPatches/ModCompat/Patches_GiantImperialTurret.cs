using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_GiantImperialTurret
    {
        static Patches_GiantImperialTurret()
        {
            if (ModCompat.GiantImperialTurret)
            {
                VMF_Harmony.PatchCategory("VMF_Patches_GiantImperialTurret");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_GiantImperialTurret")]
    [HarmonyPatch]
    public static class Patch_Building_TurretGunNonSnap_TryFindNewTarget
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("BreadMoProjOffset.Building_TurretGunNonSnap");
            var methods = type.GetDeclaredMethods();
            return methods.Where(m => m.Name.Contains("<TryFindNewTarget>") || m.Name.Contains("<>"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_GiantImperialTurret")]
    [HarmonyPatch("BreadMoProjOffset.Building_TurretGunNonSnap", "IsValidTarget")]
    public static class Patch_Building_TurretGunNonSnap_IsValidTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_GiantImperialTurret")]
    [HarmonyPatch("BreadMoProjOffset.Building_TurretGunNonSnap", "TryFindNewTarget")]
    public static class Patch_Building_TurretGunNonSnap_TryFindNewTarget2
    {
        public static void Postfix(Building_TurretGun __instance, ref float ___curAngle, LocalTargetInfo ___currentTargetInt, LocalTargetInfo __result)
        {
            if (!___currentTargetInt.IsValid && __result.IsValid && __instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                ___curAngle = Ext_Math.RotateAngle(___curAngle, vehicle.FullRotation.AsAngle);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_GiantImperialTurret")]
    [HarmonyPatch("BreadMoProjOffset.Building_TurretGunNonSnap", "Tick")]
    public static class Patch_Building_TurretGunNonSnap_Tick
    {
        public static void Prefix(ref bool __state, LocalTargetInfo ___currentTargetInt)
        {
            __state = ___currentTargetInt.IsValid;
        }

        public static void Postfix(Building_TurretGun __instance, ref float ___curAngle, bool __state, LocalTargetInfo ___currentTargetInt)
        {
            if (!___currentTargetInt.IsValid && __state && __instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                ___curAngle = Ext_Math.RotateAngle(___curAngle, -vehicle.FullRotation.AsAngle);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_GiantImperialTurret")]
    [HarmonyPatch("BreadMoProjOffset.AttackTargetFinderAngle", "BestAttackTarget")]
    public static class Patch_AttackTargetFinderAngle_BestAttackTarget
    {
        public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Vector3 angle, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canTakeTargetsCloserThanEffectiveMinRange, ref IAttackTarget __result)
        {
            if (working) return;

            var map = searcher.Thing.Map;
            var pos = searcher.Thing.Position;
            var maps = map.BaseMapAndVehicleMaps().Except(map);

            if (maps.Any())
            {
                try
                {
                    working = true;
                    var basePos = searcher.Thing.PositionOnBaseMap();
                    foreach (var map2 in maps)
                    {
                        searcher.Thing.VirtualMapTransfer(map2, map2.IsVehicleMapOf(out var vehicle) ? basePos.ToVehicleMapCoord(vehicle) : basePos);
                        var target = (IAttackTarget)BestAttackTarget(null, searcher, flags, angle, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canTakeTargetsCloserThanEffectiveMinRange);
                        if (__result == null || target != null && (__result.Thing.Position - searcher.Thing.Position).LengthHorizontalSquared > (target.Thing.PositionOnBaseMap() - searcher.Thing.PositionOnBaseMap()).LengthHorizontalSquared)
                        {
                            __result = target;
                        }
                    }
                }
                finally
                {
                    working = false;
                    searcher.Thing.VirtualMapTransfer(map, pos);
                }
            }
        }

        private static bool working;

        private static FastInvokeHandler BestAttackTarget = MethodInvoker.GetHandler(AccessTools.Method("BreadMoProjOffset.AttackTargetFinderAngle:BestAttackTarget"));
    }
}
