using CombatExtended;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using VehicleInteriors;
using VehicleInteriors.VMF_HarmonyPatches;
using Verse;

namespace VMF_CEPatch
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_CE
    {
        static Patches_CE()
        {
            var m_GetShootingTargetScore = AccessTools.Method(typeof(AttackTargetFinderOnVehicle), "GetShootingTargetScore");
            var m_GetShootingTargetScore_Postfix = AccessTools.Method("CombatExtended.HarmonyCE.Harmony_AttackTargetFinder+Harmony_AttackTargetFinder_GetShootingTargetScore:Postfix");
            VMF_Harmony.Instance.Patch(m_GetShootingTargetScore, null, m_GetShootingTargetScore_Postfix);

            VMF_Harmony.Instance.PatchCategory("VMF_Patches_CE");
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "ShotSpeed", MethodType.Getter)]
    public static class Patch_Verb_LaunchProjectileCE_ShotSpeed
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "LightingTracker", MethodType.Getter)]
    public static class Patch_Verb_LaunchProjectileCE_LightingTracker
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftVecReportFor), typeof(LocalTargetInfo))]
    public static class Patch_Verb_LaunchProjectileCE_ShiftVecReportFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.AdjustShotHeight))]
    public static class Patch_Verb_LaunchProjectileCE_AdjustShotHeight
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "GetHighestCoverAndSmokeForTarget")]
    public static class Patch_Verb_LaunchProjectileCE_GetHighestCoverAndSmokeForTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_GetFirstPawn = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetFirstPawn));
            var m_GetFirstPawnAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetFirstPawnAcrossMaps));
            var m_GetCover = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetCover));
            var m_GetCoverOnThingMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetCoverOnThingMap));
            var codes = instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(m_GetFirstPawn, m_GetFirstPawnAcrossMaps)
                .MethodReplacer(m_GetCover, m_GetCoverOnThingMap).ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_GetCoverOnThingMap));
            codes.Insert(pos, CodeInstruction.LoadLocal(1));
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.CanHitTarget))]
    public static class Patch_Verb_LaunchProjectileCE_CanHitTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "CanHitTargetFrom")]
    [HarmonyPatch(new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(string) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    public static class Patch_Verb_LaunchProjectileCE_CanHitTargetFrom
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "Retarget")]
    public static class Patch_Verb_LaunchProjectileCE_Retarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_Verb_CanHitFromCellIgnoringRange = AccessTools.Method(typeof(Verb), "CanHitFromCellIgnoringRange");
            var m_VerbOnVehicleUtility_CanHitFromCellIgnoringRange = AccessTools.Method(typeof(VerbOnVehicleUtility), "CanHitFromCellIgnoringRange");
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(m_Verb_CanHitFromCellIgnoringRange, m_VerbOnVehicleUtility_CanHitFromCellIgnoringRange);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
    public static class Patch_Verb_LaunchProjectileCE_TryCastShot
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryFindCEShootLineFromTo))]
    public static class Patch_Verb_LaunchProjectileCE_TryFindCEShootLineFromTo
    {
        public static bool Prefix(Verb_LaunchProjectileCE __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, ref bool __result)
        {
            if (__instance.caster.IsOnVehicleMapOf(out _) || (targ.HasThing && targ.Thing.Map != __instance.caster.Map))
            {
                __result = __instance.TryFindCEShootLineFromToOnVehicle(root, targ, out resultingLine);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.AimAngle), MethodType.Getter)]
    public static class Patch_Verb_ShootCE_AimAngle
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.WarmupComplete))]
    public static class Patch_Verb_ShootCE_WarmupComplete
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.CanHitTargetFrom))]
    public static class Patch_Verb_ShootCE_CanHitTargetFrom
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.RecalculateWarmupTicks))]
    public static class Patch_Verb_ShootCE_RecalculateWarmupTicks
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.RayCast))]
    public static class Patch_ProjectileCE_RayCast
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing).ToList();
            var m_ThingsListAtFast = AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAtFast), new Type[] { typeof(IntVec3) });
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsListAtFast)) + 1;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Thing_Map),
                CodeInstruction.LoadLocal(16),
                CodeInstruction.Call(typeof(Patch_ProjectileCE_RayCast), nameof(Patch_ProjectileCE_RayCast.AddThingList))
            });
            return codes;
        }

        public static List<Thing> AddThingList(List<Thing> list, Map map, IntVec3 c)
        {
            tmpList.Clear();
            tmpList.AddRange(list);
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            foreach (var map2 in maps)
            {
                var c2 = c;
                if (map2.IsVehicleMapOf(out var vehicle))
                {
                    c2 = c.ToVehicleMapCoord(vehicle);
                }
                if (c2.InBounds(map2))
                {
                    tmpList.AddRange(map2.thingGrid.ThingsListAtFast(c2));
                }
            }
            return tmpList;
        }

        private static readonly List<Thing> tmpList = new List<Thing>();
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(ProjectileCE), "CheckIntercept")]
    public static class Patch_ProjectileCE_CheckIntercept
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(ProjectileCE), "CheckForCollisionBetween")]
    public static class Patch_ProjectileCE_CheckForCollisionBetween
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 4);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Thing_Map),
                CodeInstruction.Call(typeof(Patch_ProjectileCE_CheckForCollisionBetween), nameof(Patch_ProjectileCE_CheckForCollisionBetween.AddThingList))
            });
            return codes;
        }

        private static List<Thing> AddThingList(List<Thing> list, Map map)
        {
            tmpList.Clear();
            tmpList.AddRange(list);
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            tmpList.AddRange(maps.SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor)));
            return tmpList;
        }

        private static readonly List<Thing> tmpList = new List<Thing>();
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(ProjectileCE), "CheckCellForCollision")]
    public static class Patch_ProjectileCE_CheckCellForCollision
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap).ToList();
            var m_ThingsListAtFast = AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAtFast), new Type[] { typeof(IntVec3) });
            var pos = 0;
            for (var i = 0; i < 2; i++)
            {
                pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsListAtFast)) + 1;
                codes.InsertRange(pos, new[]
                {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Thing_Map),
                CodeInstruction.LoadArgument(1),
                CodeInstruction.Call(typeof(Patch_ProjectileCE_RayCast), nameof(Patch_ProjectileCE_RayCast.AddThingList))
            });
            }
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Launch), typeof(Thing), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Thing), typeof(float))]
    public static class Patch_ProjectileCE_Launch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.ImpactSomething))]
    public static class Patch_ProjectileCE_ImpactSomething
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var m_ThingsListAt = AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAt));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsListAt)) + 1;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Thing_Map),
                CodeInstruction.LoadLocal(0),
                CodeInstruction.Call(typeof(Patch_ProjectileCE_RayCast), nameof(Patch_ProjectileCE_RayCast.AddThingList))
            });
            var m_GetFirstPawn = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetFirstPawn));
            var m_GetFirstPawnAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetFirstPawnAcrossMaps));
            return codes.MethodReplacer(m_GetFirstPawn, m_GetFirstPawnAcrossMaps);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryFindNewTarget))]
    public static class Patch_Building_TurretGunCE_TryFindNewTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var f_allBuildingsColonist = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_allBuildingsColonist));
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.g_Thing_Map),
                CodeInstruction.Call(typeof(Patch_Building_TurretGunCE_TryFindNewTarget), nameof(Patch_Building_TurretGunCE_TryFindNewTarget.AddBuildingList))
            });
            return codes;
        }

        private static List<Building> AddBuildingList(List<Building> list, Map map)
        {
            tmpList.Clear();
            tmpList.AddRange(list);
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            tmpList.AddRange(maps.SelectMany(m => m.listerBuildings.allBuildingsColonist));
            return tmpList;
        }

        private static readonly List<Building> tmpList = new List<Building>();
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch]
    public static class Patch_Building_TurretGunCE_TryFindNewTarget_Predicate
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes<MethodInfo>(typeof(Building_TurretGunCE), t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("TryFindNewTarget")));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CE")]
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.OrderAttack))]
    public static class Patch_Building_TurretGunCE_OrderAttack
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(ExplosionCE), nameof(ExplosionCE.StartExplosionCE))]
    public static class Patch_ExplosionCE_StartExplosionCE
    {
        public static void Postfix(ExplosionCE __instance, SoundDef explosionSound, List<Thing> ignoredThings)
        {
            if (__instance is ExplosionCEAcrossMaps explosion)
            {
                explosion.StartExplosionCEOnVehicle(explosionSound, ignoredThings);
            }
        }
    }
}
