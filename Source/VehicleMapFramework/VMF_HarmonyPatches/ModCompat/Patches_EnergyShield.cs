﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_EnergyShield
{
    public const string Category = "VMF_Patches_EnergyShield";

    public const string CategoryCE = "VMF_Patches_EnergyShieldCECompat";

    static Patches_EnergyShield()
    {
        if (ModCompat.EnergyShield.Active)
        {
            VMF_Harmony.PatchCategory(Category);

            if (ModCompat.EnergyShield.CECompat)
            {
                VMF_Harmony.PatchCategory(CategoryCE);
            }
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillInterceptOrbitalStrike")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_AllBuildingsColonistOfClass = AccessTools.Method(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass), generics: [ModCompat.EnergyShield.Building_Shield]);
        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (instruction.Calls(m_AllBuildingsColonistOfClass))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.Call(typeof(Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike), nameof(AddBuildings));
            }
        }
    }

    private static IEnumerable<Building> AddBuildings(IEnumerable<Building> buildings, MapComponent component)
    {
        return buildings
            .Concat(VehiclePawnWithMapCache.AllVehiclesOn(component.map)
            .SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist
            .Where(b => ModCompat.EnergyShield.Building_Shield.IsAssignableFrom(b.GetType()))));

    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillInterceptExplosion")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillInterceptExplosion
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillInterceptExplosionAffectCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillInterceptExplosionAffectCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillDropPodBeIntercepted")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillDropPodBeIntercepted
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillProjectileBeBlocked")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillProjectileBeBlocked
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "costShield")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_costShield
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "WillInterceptOrbitalStrike")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_WillInterceptOrbitalStrike
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "WillInterceptExplosionAffectCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_WillInterceptExplosionAffectCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "WillInterceptExplosion")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_WillInterceptExplosion
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "WillInterceptDropPod")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_WillInterceptDropPod
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "WillProjectileBeBlocked")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_WillProjectileBeBlocked
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShield")]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_ShieldGenerator_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (instruction.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent));
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingBaseMapCoord2);
            }
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShieldCECompat")]
[HarmonyPatch("EnergyShieldCECompat.PatchProjectileCE", "TickPostfix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PatchProjectileCE_TickPostfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_AllBuildingsColonistOfClass = AccessTools.Method(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass), generics: [ModCompat.EnergyShield.Building_Shield]);
        foreach (var instruction in instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap))
        {
            yield return instruction;

            if (instruction.Calls(m_AllBuildingsColonistOfClass))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.Call(typeof(Patch_PatchProjectileCE_TickPostfix), nameof(ReplaceBuildings));
            }
        }
    }

    private static IEnumerable<Building> ReplaceBuildings(IEnumerable<Building> buildings, Thing projectile)
    {
        return buildings.Concat(VehiclePawnWithMapCache.AllVehiclesOn(projectile.Map)
            .SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist
            .Where(b => ModCompat.EnergyShield.Building_Shield.IsAssignableFrom(b.GetType()))));

    }
}

[HarmonyPatchCategory("VMF_Patches_EnergyShieldCECompat")]
[HarmonyPatch("cn.zhuzijun.EnergyShieldCECompat.ZMod", "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_ZMod_CheckIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_PatchProjectileCE_TickPostfix.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EnergyShieldCECompat")]
[HarmonyPatch("cn.zhuzijun.EnergyShieldCECompat.ZMod", "ImpactSomethingCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_ZMod_ImpactSomethingCallback
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_PatchProjectileCE_TickPostfix.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EnergyShieldCECompat")]
[HarmonyPatch("cn.zhuzijun.EnergyShieldCECompat.ZMod", "ShieldZonesCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_ZMod_ShieldZonesCallback
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_PatchProjectileCE_TickPostfix.Transpiler(instructions);
}
