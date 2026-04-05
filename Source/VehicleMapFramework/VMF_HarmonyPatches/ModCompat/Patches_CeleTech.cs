using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_CeleTech
{
    static Patches_CeleTech()
    {
        if (CeleTech)
        {
            VMF_Harmony.PatchCategory(PatchCategories.CeleTechArsenal);
            Patch_Projectile_CheckForFreeInterceptBetween.Prefixes.Add(Patch_CheckForFreeInterceptBetween_Prefix.PrefixPatch);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "OrderAttack")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_OrderAttack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "IsTargetStillValid")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_IsTargetStillValid
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "TryFindNewTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_CMCTurretGun_TryFindNewTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Map), nameof(Map.attackTargetsCache))))
            .RemoveInstruction()
            .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.GetPotentialTargetsFor))))
            .Set(OpCodes.Call, AccessTools.Method(typeof(Patch_Building_CMCTurretGun_TryFindNewTarget), nameof(GetPotentialTargetsForCrossMap)))
            .InstructionEnumeration();
    }

    private static readonly List<IAttackTarget> tmpList = [];
    
    private static List<IAttackTarget> GetPotentialTargetsForCrossMap(Map map, IAttackTargetSearcher attackTargetSearcher)
    {
        tmpList.Clear();
        foreach (var map2 in map.BaseMapAndVehicleMaps(true))
        {
            tmpList.AddRange(map2.attackTargetsCache.GetPotentialTargetsFor(attackTargetSearcher));
        }
        return tmpList;
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "TestForTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_TestForTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "CanTargetNow")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_CanTargetNow
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "ScoreTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_ScoreTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
            .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
            .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldarg_0), CodeMatch.Calls(CachedMethodInfo.g_Thing_Map),
                CodeMatch.Calls(AccessTools.Method(typeof(CoverUtility), nameof(CoverUtility.CalculateOverallBlockChance))))
            .SetInstruction(CodeInstruction.LoadArgument(2))
            .Advance(-1)
            .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnAnotherThingMap)
            .Insert(CodeInstruction.LoadArgument(2))
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun_AAAS", "CanEngageTarget")]
[HarmonyPatch([typeof(LocalTargetInfo)])]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_AAAS_CanEngageTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_PDBattery", "TryFindNewTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_PDBattery_TryFindNewTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.AddAllBuildingsColonistForThingInstance();
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_PDBattery_TryFindNewTarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.Building_PDBattery", "TOT_DLL_test"), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>"));
        });
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CMCTurretTop", "DrawTurret")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CMCTurretTop_DrawTurret
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new CodeMatcher(instructions, generator)
            .AddAltitudeFor(out _,
                getInstance: [CodeInstruction.LoadArgument(0),
                    CodeInstruction.LoadField(
                        GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.CMCTurretTop", "TOT_DLL_test"), "parentTurret")])
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CMCTurretTop", "ForceFaceTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_CMCTurretTop_ForceFaceTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CMCTurretTop", "TurretTopTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_CMCTurretTop_TurretTopTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_FCradar", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_FCradar_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new CodeMatcher(instructions, generator)
            .AddAltitudeFor(out _,
                getInstance: [CodeInstruction.LoadArgument(0), CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent))])
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_CMCShield", "Draw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_CMCShield_Draw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_PrismTowerTop", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_PrismTowerTop_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_TraderShuttle", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_TraderShuttle_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_UAV", "CompTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_UAV_CompTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_FloatingGunRework", "CompTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_FloatingGunRework_CompTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_Verb_LaunchProjectile_TryCastShot.Transpiler(instructions);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "Retarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_Retarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_Verb_LauncherProjectileSwitchFire_Retarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "TOT_DLL_test"), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<Retarget>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "CanHitFromCellIgnoringRange")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_CanHitFromCellIgnoringRange
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_Laser_Instant", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Instant_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_Laser_Instant_UAV", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Instant_UAV_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_Laser_Sustain", "BurstingTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Sustain_BurstingTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_Laser_Sustain", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Laser_Sustain_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_PlasmaIncinerator", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_PlasmaIncinerator_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_RocketShoot", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_RocketShoot_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_ShootDronePos", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootDronePos_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_Shoot_UAV", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Shoot_UAV_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.HarmonyPatches+Harmony_CheckForFreeInterceptBetween", "Prefix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_CheckForFreeInterceptBetween_Prefix
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static bool PrefixPatch(Projectile __instance, Vector3 lastExactPos, Vector3 newExactPos, ref bool __result)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CompFullProjectileInterceptor", "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompFullProjectileInterceptor_CheckIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CompFullProjectileInterceptor", "GetCurrentAlpha")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompFullProjectileInterceptor_GetCurrentAlpha
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CompFullProjectileInterceptor", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompFullProjectileInterceptor_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Map), nameof(Map.attackTargetsCache))))
            .RemoveInstruction()
            .Set(OpCodes.Call, AccessTools.Method(typeof(Patch_CompFullProjectileInterceptor_PostDraw), nameof(TargetsHostileToColonyCrossMap)))
            .InstructionEnumeration();
    }

    private static readonly HashSet<IAttackTarget> tmpSet = [];
    
    private static HashSet<IAttackTarget> TargetsHostileToColonyCrossMap(Map map)
    {
        tmpSet.Clear();
        foreach (var map2 in map.BaseMapAndVehicleMaps(true))
        {
            tmpSet.AddRange(map2.attackTargetsCache.TargetsHostileToColony);
        }
        return tmpSet;
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CompFullProjectileInterceptor", "PostDrawExtraSelectionOverlays")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompFullProjectileInterceptor_PostDrawExtraSelectionOverlays
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Patch_CompFullProjectileInterceptor_PostDraw.Transpiler(instructions);
}