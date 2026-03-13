using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CombatExtended;
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.ShiftVecReportFor), typeof(LocalTargetInfo), typeof(IntVec3))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectileCE_ShiftVecReportFor2
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
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
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
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
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
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
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_LaunchProjectileCE), "TryFindCEShootLineFromTo", [typeof(IntVec3), typeof(LocalTargetInfo), typeof(ShootLine), typeof(Vector3)], [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out])]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_LaunchProjectileCE_TryFindCEShootLineFromTo
{
    public static bool Prefix(Verb_LaunchProjectileCE __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, ref Vector3 targetPos, ref bool __result)
    {
        if (__instance.caster.IsOnVehicleMapOf(out _) ||
            targ.Thing.IsOnVehicleMapOf(out _) ||
            (__instance.caster.TryGetTargetMap(out var map) && map.IsVehicleMapOf(out _)) ||
            root.IsValid && GenSight.PointsOnLineOfSight(root, targ.Cell).Any(c => c.InBounds(__instance.caster.Map) && c.TryGetVehicleMap(__instance.caster.Map, out _)))
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Verb_ShootCE), nameof(Verb_ShootCE.CanHitTargetFrom))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCE_CanHitTargetFrom
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
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
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2);
    }

    [HarmonyPatch([typeof(GlobalTargetInfo)])]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_GlobalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap_GlobalTargetInfo)
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
                return VMF_Harmony.ReadMethodBodyWrapper(m).Any(i =>
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
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(VerbCIWS<ProjectileCE>), nameof(VerbCIWS.TryFindCEShootLineFromTo))]
[PatchLevel(Level.Cautious)]
public static class Patch_VerbCIWS_TryFindCEShootLineFromTo
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
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
        tmpList.Clear();
        tmpList.AddRange(list);
        var maps = map.BaseMapAndVehicleMaps(false);
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
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_ProjectileCE_CheckIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), "CheckForCollisionBetween")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ProjectileCE_CheckForCollisionBetween
{
    private static readonly List<Thing> tmpList = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 4);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Thing_Map),
            CodeInstruction.Call(typeof(Patch_ProjectileCE_CheckForCollisionBetween), nameof(AddThingList))
        ]);
        return codes;
    }

    private static List<Thing> AddThingList(List<Thing> list, Map map)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        var maps = map.BaseMapAndVehicleMaps(false);
        tmpList.AddRange(maps.SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor)));
        return tmpList;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(ProjectileCE), "CheckCellForCollision")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ProjectileCE_CheckCellForCollision
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap).ToList();
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
    private static readonly List<Building> tmpList = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var f_allBuildingsColonist = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_allBuildingsColonist)) + 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Thing_Map),
            CodeInstruction.Call(typeof(Patch_Building_TurretGunCE_TryFindNewTarget), nameof(AddBuildingList))
        ]);
        return codes;
    }

    private static List<Building> AddBuildingList(List<Building> list, Map map)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        var maps = map.BaseMapAndVehicleMaps(false);
        tmpList.AddRange(maps.SelectMany(m => m.listerBuildings.allBuildingsColonist));
        return tmpList;
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
                return VMF_Harmony.ReadMethodBodyWrapper(m).Any(i =>
                    CachedMethodInfo.g_Thing_Position.Equals(i.Value));
            }));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtended)]
[HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.OrderAttack))]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunCE_OrderAttack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
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
public static class Patch_Verb_LaunchProjectileCE_ShiftTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
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