using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using CombatExtended;
using CombatExtended.Compatibility;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_CE
{
    static Patches_CE()
    {
        var method = AccessTools.Method(typeof(AttackTargetFinderOnVehicle), "GetShootingTargetScore");
        var patch = AccessTools.Method("CombatExtended.HarmonyCE.Harmony_AttackTargetFinder+Harmony_AttackTargetFinder_GetShootingTargetScore:Postfix");
        VMF_Harmony.Instance.Patch(method, postfix: patch);

        VMF_Harmony.PatchCategory(PatchCategories.CombatExtended);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool))]
[PatchLevel(Level.Safe)]
public static class Patch_GenSpawn_Spawn
{
    public static void Prefix(Thing newThing, ref Map map)
    {
        if (newThing is ProjectileCE)
        {
            map = map.BaseMap();
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), "ShotSpeed", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_ShotSpeed
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), "LightingTracker", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_LightingTracker
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftVecReportFor), typeof(LocalTargetInfo))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_ShiftVecReportFor1
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftVecReportFor), typeof(LocalTargetInfo), typeof(IntVec3))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_ShiftVecReportFor2
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.AdjustShotHeight))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_AdjustShotHeight
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), "GetHighestCoverAndSmokeForTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_LaunchProjectileCE_GetHighestCoverAndSmokeForTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_GetFirstPawn = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetFirstPawn));
        var m_GetFirstPawnAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetFirstPawnAcrossMaps));
        var m_GetCover = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetCover));
        var m_GetCoverOnThingMap = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetCoverOnThingMap));
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(m_GetFirstPawn, m_GetFirstPawnAcrossMaps)
            .MethodReplacer(m_GetCover, m_GetCoverOnThingMap).ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_GetCoverOnThingMap));
        codes.Insert(pos, CodeInstruction.LoadLocal(1));
        return codes;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.CanHitTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_CanHitTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.CanHitTargetFrom))]
[HarmonyPatch([typeof(IntVec3), typeof(LocalTargetInfo), typeof(string)], [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_CanHitTargetFrom
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), "Retarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_Retarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_Verb_CanHitFromCellIgnoringRange = AccessTools.Method(typeof(Verb), "CanHitFromCellIgnoringRange");
        var m_VerbOnVehicleUtility_CanHitFromCellIgnoringRange = AccessTools.Method(typeof(VerbOnVehicleUtility), "CanHitFromCellIgnoringRange");
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(m_Verb_CanHitFromCellIgnoringRange, m_VerbOnVehicleUtility_CanHitFromCellIgnoringRange);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
    }
}


[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_TryFindShootLineFromTo
{
    public static bool Prefix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, bool ignoreRange, ref bool __result)
    {
        if (__instance is Verb_LaunchProjectileCE)
            return true;
        
        if (VerbOnVehicleUtility.ShouldConsiderCrossMap(__instance.caster, root, targ))
        {
            __result = __instance.TryFindShootLineFromToOnVehicle(root, targ, out resultingLine, ignoreRange);
            return false;
        }
        return true;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), "TryFindCEShootLineFromTo", [typeof(IntVec3), typeof(LocalTargetInfo), typeof(ShootLine), typeof(Vector3)], [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out])]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_LaunchProjectileCE_TryFindCEShootLineFromTo
{
    public static bool Prefix(Verb_LaunchProjectileCE __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, ref Vector3 targetPos, ref bool __result)
    {
        if (VerbOnVehicleUtility.ShouldConsiderCrossMap(__instance.caster, root, targ))
        {
            __result = __instance.TryFindCEShootLineFromToOnVehicle(root, targ, out resultingLine, out targetPos);
            return false;
        }
        return true;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.AimAngle), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCE_AimAngle
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.WarmupComplete))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCE_WarmupComplete
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.CanHitTargetFrom))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCE_CanHitTargetFrom
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.RecalculateWarmupTicks))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCE_RecalculateWarmupTicks
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_ShootMortarCE), nameof(Verb_ShootMortarCE.ShiftVecReportFor))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootMortarCE_ShiftVecReportFor
{
    [HarmonyPatch([typeof(LocalTargetInfo), typeof(IntVec3)])]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2);
    }

    [HarmonyPatch([typeof(GlobalTargetInfo)])]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_GlobalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned_GlobalTargetInfo)
            .MethodReplacer(CachedMethodInfo.g_GlobalTargetInfo_Map, CachedMethodInfo.m_BaseMap_GlobalTargetInfo)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_VerbCIWS_TryFindNewTarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        var m_ProjectilesAt = AccessTools.Method(typeof(ProjectileCE_CIWS), nameof(ProjectileCE_CIWS.ProjectilesAt));
        return AccessTools.FindIncludingInnerTypes(typeof(VerbCIWS<ProjectileCE>), t =>
        {
            if (!t.IsGenericTypeDefinition) return null;
            return AccessTools.FirstMethod(t.MakeGenericType(typeof(ProjectileCE)), m =>
            {
                if (!m.Name.Contains("<TryFindNewTarget>")) return false;
                return PatchHelper.ReadMethodBodyWrapper(m).Any(i =>
                    m_ProjectilesAt.Equals(i.Value));
            });
        });
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_ProjectilesAt = AccessTools.Method(typeof(ProjectileCE_CIWS), nameof(ProjectileCE_CIWS.ProjectilesAt));
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map), CodeMatch.Calls(m_ProjectilesAt))
            .Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing)
            .InstructionEnumeration()
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(VerbCIWS<ProjectileCE>), nameof(VerbCIWS.TryFindCEShootLineFromTo))]
[PatchLevel(Level.Cautious)]
public static class Patch_VerbCIWS_ProjectileCE_TryFindCEShootLineFromTo
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

// VerbCIWS<ProjectileCE>とパッチ結果が共有されなかったため、VerbCIWS<Skyfaller>にも同様のパッチを適用する必要があった
[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(VerbCIWS<Skyfaller>), nameof(VerbCIWS.TryFindCEShootLineFromTo))]
[PatchLevel(Level.Cautious)]
public static class Patch_VerbCIWS_Skyfaller_TryFindCEShootLineFromTo2
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(VerbCIWSProjectile), nameof(VerbCIWSProjectile.Targets), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_VerbCIWSProjectile_Targets
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(VerbCIWSSkyfaller), nameof(VerbCIWSSkyfaller.Targets), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_VerbCIWSSkyfaller_Targets
{
    public static IEnumerable<Skyfaller> Postfix(IEnumerable<Skyfaller> values, VerbCIWSSkyfaller __instance)
    {
        if (values is null) yield break;
        foreach (var value in values) yield return value;
        foreach (var map in __instance.Caster.Map.BaseMapAndVehicleMaps(false))
        {
            foreach (var transporter in map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter))
            {
                if (transporter is Skyfaller skyfaller)
                    yield return skyfaller;
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_CompCIWSTarget_Targets
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FirstMethod(typeof(CompCIWSTarget), m => m.Name == nameof(CompCIWSTarget.Targets) && !m.IsGenericMethodDefinition);
    }
    
    public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Map map)
    {
        foreach (var value in values) yield return value;
        foreach (var map2 in map.BaseMapAndVehicleMaps(false))
        {
            foreach (var transporter in map2.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter))
            {
                if (transporter is Skyfaller skyfaller)
                    yield return skyfaller;
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(CE_Utility), nameof(CE_Utility.GetBoundsFor), typeof(Thing))]
[PatchLevel(Level.Safe)]
public static class Patch_CE_Utility_GetBoundsFor
{
    public static void Prefix(Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && !vehicle.Spawned)
        {
            VehiclePawnWithMapCache.CacheMode = true;
        }
    }
    
    public static void Finalizer() => VehiclePawnWithMapCache.CacheMode = false;
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.RayCast))]
[PatchLevel(Level.Sensitive)]
public static class Patch_ProjectileCE_RayCast
{
    private static readonly List<Thing> tmpList = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing).ToList();
        var m_ThingsListAtFast = AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAtFast), [typeof(IntVec3)]);
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsListAtFast)) + 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Thing_Map),
            CodeInstruction.LoadLocal(16),
            CodeInstruction.Call(typeof(Patch_ProjectileCE_RayCast), nameof(AddThingList))
        ]);
        return codes;
    }

    public static List<Thing> AddThingList(List<Thing> list, Map map, IntVec3 c)
    {
        var vehicles = VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(map);
        if (vehicles.Length == 0) return list;
        
        tmpList.Clear();
        tmpList.AddRange(list);
        foreach (var vehicle in vehicles)
        {
            var c2 = c.ToVehicleMapCoord(vehicle);
            if (c2.InBounds(vehicle.VehicleMap))
            {
                tmpList.AddRange(vehicle.VehicleMap.thingGrid.ThingsListAtFast(c2));
            }
        }
        return tmpList;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(BlockerRegistry), nameof(BlockerRegistry.ImpactSomethingCallback))]
[PatchLevel(Level.Safe)]
public static class Patch_BlockerRegistry_ImpactSomethingCallback
{
    public static List<Func<ProjectileCE, Thing, bool>> Callbacks { get; } = [];

    public static void Postfix(ProjectileCE projectile, Thing launcher, ref bool __result)
    {
        if (__result || projectile is not { Spawned: true }) return;

        if (projectile.ExactPosition.TryGetVehicleMap(projectile.Map, out var vehicle, VehicleMapFlag.None))
        {
            projectile.TargetMap = vehicle.VehicleMap;
            try
            {
                for (var i = 0; i < Callbacks.Count; i++)
                {
                    if (Callbacks[i](projectile, launcher))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            finally
            {
                projectile.RemoveTargetInfo();
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), "CheckIntercept")]
[PatchLevel(Level.Mandatory)]
public static class Patch_ProjectileCE_CheckIntercept
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static bool CheckIntercept(ProjectileCE instance, Thing interceptorThing, CompProjectileInterceptor interceptorComp, bool withDebug = false)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(BlockerRegistry), nameof(BlockerRegistry.CheckForCollisionBetweenCallback))]
[PatchLevel(Level.Safe)]
public static class Patch_BlockerRegistry_CheckForCollisionBetweenCallback
{
    public delegate bool Prefix(ProjectileCE projectile, ref bool __result);
    public static List<Prefix> Prefixes { get; } = [];
    public static List<Func<ProjectileCE, Vector3, Vector3, bool>> Callbacks { get; } = [VanillaIntercept];

    public static void Postfix(ProjectileCE projectile, Vector3 from, Vector3 to, ref bool __result)
    {
        if (__result || projectile is not { Spawned: true }) return;

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(projectile.Map))
        {
            projectile.TargetMap = vehicle.VehicleMap;
            try
            {
                for (var i = 0; i < Prefixes.Count; i++)
                {
                    if (!Prefixes[i](projectile, ref __result))
                    {
                        return;
                    }
                }
                for (var i = 0; i < Callbacks.Count; i++)
                {
                    if (Callbacks[i](projectile, from, to))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            finally
            {
                projectile.RemoveTargetInfo();
            }
        }
    }

    private static bool VanillaIntercept(ProjectileCE projectile, Vector3 from, Vector3 to)
    {
        var list = projectile.TargetMapOrThingMap.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
        for (var i = 0; i < list.Count; ++i)
        {
            if (Patch_ProjectileCE_CheckIntercept.CheckIntercept(projectile, list[i], list[i].TryGetComp<CompProjectileInterceptor>()))
            {
                if (projectile.def.projectile.flyOverhead)
                {
                    projectile.Destroy();
                    return true;
                }
                projectile.landed = true;
                projectile.Impact(null);
                return true;
            }
        }

        return false;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(BlockerRegistry), nameof(BlockerRegistry.CheckCellForCollisionCallback))]
[PatchLevel(Level.Safe)]
public static class Patch_BlockerRegistry_CheckCellForCollisionCallback
{
    public static List<Func<ProjectileCE, IntVec3, Thing, bool>> Callbacks { get; } = [];

    public static void Postfix(ProjectileCE projectile, IntVec3 cell, Thing launcher, ref bool __result)
    {
        if (__result || projectile is not { Spawned: true }) return;
        
        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(projectile.Map))
        {
            projectile.TargetMap = vehicle.VehicleMap;
            var cell2 = cell.ToVehicleMapCoord(vehicle);
            if (!cell2.InBounds(vehicle.VehicleMap)) continue;
            try
            {
                for (var i = 0; i < Callbacks.Count; i++)
                {
                    if (Callbacks[i](projectile, cell2, launcher))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            finally
            {
                projectile.RemoveTargetInfo();
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(BlockerRegistry), nameof(BlockerRegistry.ShieldZonesCallback))]
[PatchLevel(Level.Safe)]
public static class Patch_BlockerRegistry_ShieldZonesCallback
{
    public static List<Func<Thing, IEnumerable<IEnumerable<IntVec3>>>> Callbacks { get; } = [VanillaZones];

    public static IEnumerable<IEnumerable<IntVec3>> Postfix(IEnumerable<IEnumerable<IntVec3>> values, Thing thing)
    {
        foreach (var value in values) yield return value;

        if (thing is not { Spawned: true }) yield break;

        var thingMap = thing.Map;
        var angleA = thingMap.IsVehicleMapOf(out var vehicle) ? vehicle.FullAngle : 0f;
        var inBounds = (IntVec3 c) => c.InBounds(thingMap);
        
        foreach (var map in thing.Map.BaseMapAndVehicleMaps(false))
        {
            using var _ = new VirtualTeleporter(thing, map);
            var angleB = map.IsVehicleMapOf(out var vehicle2) ? vehicle2.FullAngle : 0f;
            var originB = vehicle2 is not null ? Vector3.zero.ToBaseMapCoord(vehicle2) : Vector3.zero;
            var originBinA = vehicle is not null ? originB.ToVehicleMapCoord(vehicle) : originB;
            var relAng = (angleB - angleA) * Mathf.Deg2Rad;
            var sin = Mathf.Sin(relAng);
            var cos = Mathf.Cos(relAng);
            var transform = (IntVec3 c) =>
            {
                var nx = c.x * cos - c.z * sin;
                var nz = c.x * sin + c.z * cos;
                return new IntVec3(Mathf.RoundToInt(nx + originBinA.x), 0, Mathf.RoundToInt(nz + originBinA.z));
            };
            
            for (var i = 0; i < Callbacks.Count; i++)
            {
                foreach (var cells in Callbacks[i](thing).ToArray()) // テレポート中に列挙を確定
                {
                    yield return cells.Select(transform).Where(inBounds);
                }
            }
        }
    }

    private static IEnumerable<IEnumerable<IntVec3>> VanillaZones(Thing thing)
    {
        foreach (var interceptor in thing.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor))
        {
            var comp = interceptor.TryGetComp<CompProjectileInterceptor>();
            if (comp.Active && (comp.Props.interceptNonHostileProjectiles || !interceptor.HostileTo(thing)))
            {
                yield return GenRadial.RadialCellsAround(interceptor.Position, comp.Props.radius, true);
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), "CheckCellForCollision")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ProjectileCE_CheckCellForCollision
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned).ToList();
        var m_ThingsListAtFast = AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAtFast), [typeof(IntVec3)]);
        var pos = 0;
        for (var i = 0; i < 2; i++)
        {
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsListAtFast)) + 1;
            codes.InsertRange(pos,
            [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Thing_Map),
            CodeInstruction.LoadArgument(1),
            CodeInstruction.Call(typeof(Patch_ProjectileCE_RayCast), nameof(Patch_ProjectileCE_RayCast.AddThingList))
        ]);
        }
        return codes;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.Launch), typeof(Thing), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Thing), typeof(float))]
[PatchLevel(Level.Cautious)]
public static class Patch_ProjectileCE_Launch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.ImpactSomething))]
[PatchLevel(Level.Sensitive)]
public static class Patch_ProjectileCE_ImpactSomething
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var m_ThingsListAt = AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAt));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(m_ThingsListAt)) + 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Thing_Map),
            CodeInstruction.LoadLocal(0),
            CodeInstruction.Call(typeof(Patch_ProjectileCE_RayCast), nameof(Patch_ProjectileCE_RayCast.AddThingList))
        ]);
        var m_GetFirstPawn = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetFirstPawn));
        var m_GetFirstPawnAcrossMaps = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.GetFirstPawnAcrossMaps));
        return codes.MethodReplacer(m_GetFirstPawn, m_GetFirstPawnAcrossMaps);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryFindNewTarget))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunCE_TryFindNewTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.AddAllBuildingsColonistForThingInstance();
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunCE_TryFindNewTarget_Predicate
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(Building_TurretGunCE), t => t.GetDeclaredMethods()
            .FirstOrDefault(m =>
            {
                if (!m.Name.Contains("TryFindNewTarget")) return false;
                return PatchHelper.ReadMethodBodyWrapper(m).Any(i =>
                    CachedMethodInfo.g_Thing_Position.Equals(i.Value));
            }));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.OrderAttack))]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunCE_OrderAttack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ExplosionCE), nameof(ExplosionCE.StartExplosionCE))]
[PatchLevel(Level.Safe)]
public static class Patch_ExplosionCE_StartExplosionCE
{
    public static void Postfix(ExplosionCE __instance)
    {
        if (__instance is ExplosionCEAcrossMaps explosion)
        {
            explosion.StartExplosionCEOnVehicle();
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftTarget), typeof(ShiftVecReport), typeof(bool), typeof(bool))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_ShiftTarget1
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftTarget),
    [typeof(ShiftVecReport), typeof(Vector3), typeof(float), typeof(bool), typeof(bool)],
    [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_LaunchProjectileCE_ShiftTarget2
{
    public static void Prefix(Thing ___caster)
    {
        if (___caster.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned)
        {
            VehiclePawnWithMapCache.CacheMode = true;
        }
    }

    public static void Finalizer() => VehiclePawnWithMapCache.CacheMode = false;
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), "DistanceTraveled", MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_ProjectileCE_DistanceTraveled
{
    public static bool Prefix(ProjectileCE __instance, Vector2 ___origin, LocalTargetInfo ___intendedTarget, ref float __result)
    {
        if (__instance is ProjectileCE_Explosive && ___intendedTarget.Thing.IsOnVehicleMapOf(out _))
        {
            var dest = ___intendedTarget.Thing.TrueCenter();
            __result = Vector2.Distance(___origin, new Vector2(dest.x, dest.z));
            return false;
        }
        return true;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.DrawPos), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_ProjectileCE_DrawPos
{
    public static bool Prefix(ProjectileCE __instance, ref Vector3 __result)
    {
        return !__instance.TryGetDrawPos(ref __result);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.ExactRotation), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_ProjectileCE_ExactRotation
{
    public static void Postfix(ProjectileCE __instance, ref Quaternion __result)
    {
        if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            __result *= vehicle.FullAngleQuat;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), nameof(ProjectileCE.DrawRotation), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_ProjectileCE_DrawRotation
{
    public static void Postfix(ProjectileCE __instance, ref Quaternion __result)
    {
        if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            __result *= vehicle.FullAngleQuat;
        }
    }
}


[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(NonSnapAttackTargetFinder), nameof(NonSnapAttackTargetFinder.BestAttackTarget))]
[PatchLevel(Level.Safe)]
public static class Patch_NonSnapAttackTargetFinder_BestAttackTarget
{
    public static void Postfix(IAttackTargetSearcher searcher, TargetScanFlags flags, Vector3 angle, Predicate<Thing> validator, float minDist, float maxDist, ref IAttackTarget __result)
    {
        var target = NonSnapAttackTargetFinderOnVehicle.BestAttackTarget(searcher, flags, angle, validator, minDist, maxDist);
        __result = AttackTargetFinderOnVehicle.CompareTarget(__result, target, searcher);
    }
}