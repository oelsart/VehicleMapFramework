using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_UFHeavyIndustries
{
    static Patches_UFHeavyIndustries()
    {
        if (UFHeavyIndustries)
        {
            VMF_Harmony.PatchCategory(PatchCategories.UFHeavyIndustries);
            try
            {
                Patch_Projectile_CheckForFreeInterceptBetween.Prefixes.Add(
                    Patch_Patch_Projectile_CheckForFreeInterceptBetween_Prefix.PrefixPatch);
                var func = AccessTools.MethodDelegate<Func<Bombardment, Bombardment.BombardmentProjectile, bool>>(
                    AccessTools.Method("ATFieldGenerator.Patch_Bombardment_TryDoExplosion:Prefix"));
                if (func is null) throw new NullReferenceException();
                Patch_Bombardment_TryDoExplosion.Prefixes.Add(func);
            }
            catch (Exception ex)
            {
                VMF_Log.Error($"{ex}");
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("HNGT.Building_TurretGunRotateAim", "TryStartShootSomething")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunRotateAim_TryStartShootSomething
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps);
    }   
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("HNGT.Building_TurretGunRotateAim", "IsValidTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunRotateAim_IsValidTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps);
    }   
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("HNGT.Building_TurretGunRotateAim", "TryFindNewTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunRotateAim_TryFindNewTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.AddAllBuildingsColonistForThingInstance();
    }   
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunRotateAim_TryFindNewTarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        var type = GenTypes.GetTypeInAnyAssembly("HNGT.Building_TurretGunRotateAim", "HNGT");
        return AccessTools.FindIncludingInnerTypes(type, t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>"));
        });
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

// ECT版Building_TurretGunHasSpeed特有のコードに対するパッチ
[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ECT.Building_TurretGunHasSpeed", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunHasSpeed_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new CodeMatcher(instructions, generator)
            .AddAltitudeFor(out _)
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("HNGT.Building_TurretGunRotateAim", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunRotateAim_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new CodeMatcher(instructions, generator)
            .AddAltitudeFor(out _)
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ECT.Verb_ShootWithOffset", "BaseTryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithOffsetECT_BaseTryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("HNGT.Verb_BarrelWithRecoilAndFlash", "BaseTryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_BarrelWithRecoilAndFlash_BaseTryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

// [HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
// [HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "Break")]
// [PatchLevel(Level.Cautious)]
// public static class Patch_Comp_AbsoluteTerrorField_Break
// {
//     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
//     {
//         return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
//             .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
//     }
// }

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "SpawnInterceptEffect")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_AbsoluteTerrorField_SpawnInterceptEffect
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_AbsoluteTerrorField_CheckIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Patch_Projectile_CheckForFreeInterceptBetween", "Prefix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Patch_Projectile_CheckForFreeInterceptBetween_Prefix
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static bool PrefixPatch(Projectile __instance, Vector3 lastExactPos, Vector3 newExactPos, ref bool __result)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map), new CodeMatch(OpCodes.Bne_Un_S))
                .Set(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing)
                .MatchStartBackwards(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
                .Set(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing)
                .InstructionEnumeration()
                .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap)
                .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "CheckBombardmentIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_AbsoluteTerrorField_CheckBombardmentIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "CheckBeamIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_AbsoluteTerrorField_CheckBeamIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch]
public static class Patch_Beam_Launch_ATField
{
    [HarmonyPatch(typeof(Beam), nameof(Beam.Launch))]
    [PatchLevel(Level.Safe)]
    public static bool Prefix(Beam __instance, Thing launcher, LocalTargetInfo usedTarget)
    {
        if (launcher is not { Spawned: true }) return true;

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(__instance.Map))
        {
            using var _ = new VirtualTeleporter(launcher, vehicle.VehicleMap);
            if (!PatchPrefix(__instance, launcher, usedTarget))
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    [HarmonyPatch("ATFieldGenerator.Patch_Beam_Launch", "Prefix")]
    [PatchLevel(Level.Mandatory)]
    public static bool PatchPrefix(Beam __instance, Thing launcher, LocalTargetInfo usedTarget) => false;
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "CheckVerbShootBeamIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_AbsoluteTerrorField_CheckVerbShootBeamIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch]
public static class Patch_Verb_ShootBeam_HitCell_ATField
{
    [HarmonyPatch(typeof(Verb_ShootBeam), "HitCell")]
    [PatchLevel(Level.Safe)]
    public static bool Prefix(Verb_ShootBeam __instance, IntVec3 cell, IntVec3 sourceCell)
    {
        if (__instance.Caster is not { Spawned: true }) return true;

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(__instance.Caster.Map))
        {
            using var _ = new VirtualTeleporter(__instance.Caster, vehicle.VehicleMap);
            if (!PatchPrefix(__instance, cell, sourceCell))
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    [HarmonyPatch("ATFieldGenerator.Patch_Verb_ShootBeam_HitCell", "Prefix")]
    [PatchLevel(Level.Mandatory)]
    public static bool PatchPrefix(Verb_ShootBeam __instance, IntVec3 cell, IntVec3 sourceCell) => false;
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "CheckBurstingTickIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_AbsoluteTerrorField_CheckBurstingTickIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch]
public static class Patch_Verb_ShootBeam_BurstingTick_ATField
{
    [HarmonyPatch(typeof(Verb_ShootBeam), nameof(Verb_ShootBeam.BurstingTick))]
    [PatchLevel(Level.Safe)]
    public static bool Prefix(Verb_ShootBeam __instance)
    {
        if (__instance.Caster is not { Spawned: true }) return true;

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(__instance.Caster.Map))
        {
            using var _ = new VirtualTeleporter(__instance.Caster, vehicle.VehicleMap);
            if (!PatchPrefix(__instance))
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    [HarmonyPatch("ATFieldGenerator.Patch_Verb_ShootBeam_BurstingTick", "Prefix")]
    [PatchLevel(Level.Mandatory)]
    public static bool PatchPrefix(Verb_ShootBeam __instance) => false;
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.Comp_AbsoluteTerrorField", "DrawShield")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_AbsoluteTerrorField_DrawShield
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
            .InsertAfterAndAdvance(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
            .InsertAfter(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.UFHeavyIndustries)]
[HarmonyPatch("ATFieldGenerator.ATFieldManager", "DrawAllFields")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ATFieldManager_DrawAllFields
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
            .InsertAfterAndAdvance(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
            .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(MapComponent), nameof(MapComponent.map))))
            .InsertAfter(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
            .InstructionEnumeration();
    }
}