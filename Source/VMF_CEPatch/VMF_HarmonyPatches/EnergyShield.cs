using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CombatExtended;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class EnergyShield
{
    static EnergyShield()
    {
        if (ModCompat.EnergyShield.Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.EnergyShieldCECompat);
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly("cn.zhuzijun.EnergyShieldCECompat.ZMod");
                var method = AccessTools.Method(type, "ShieldZonesCallback");
                var instance = LoadedModManager.GetMod(type);
                var func = AccessTools.MethodDelegate<Func<Thing, IEnumerable<IEnumerable<IntVec3>>>>(method, instance);
                if (func is null) throw new NullReferenceException();
                Patch_BlockerRegistry_ShieldZonesCallback.Callbacks.Add(func);
            }
            catch (Exception ex)
            {
                VMF_Log.Error($"Could not register EnergyShield ShieldZones callback for CE.\n{ex}");
            }
            
            Patch_BlockerRegistry_ImpactSomethingCallback.Callbacks.Add(
                Patch_ZMod_ImpactSomethingCallback.ImpactSomethingCallback);
            Patch_BlockerRegistry_CheckCellForCollisionCallback.Callbacks.Add(
                Patch_ZMod_CheckIntercept.CheckIntercept);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.EnergyShieldCECompat)]
[HarmonyPatch("EnergyShieldCECompat.PatchProjectileCE", "TickPostfix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PatchProjectileCE_TickPostfix
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
                yield return CodeInstruction.Call(typeof(Patch_PatchProjectileCE_TickPostfix), nameof(ReplaceBuildings));
            }
        }
    }

    private static IEnumerable<Building> ReplaceBuildings(IEnumerable<Building> buildings, Thing projectile)
    {
        return buildings.Concat(VehiclePawnWithMapCache.AllVehiclesOn(projectile.Map)
            .SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist
            .Where(b => b.def.thingClass.SameOrSubclassOf(ModCompat.EnergyShield.Building_Shield))));

    }
}

[HarmonyPatchCategory(PatchCategories.EnergyShieldCECompat)]
[HarmonyPatch("cn.zhuzijun.EnergyShieldCECompat.ZMod", "CheckIntercept")]
[PatchLevel(Level.Mandatory)]
public static class Patch_ZMod_CheckIntercept
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static bool CheckIntercept(ProjectileCE projectile, IntVec3 cell, Thing launcher)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.EnergyShieldCECompat)]
[HarmonyPatch("cn.zhuzijun.EnergyShieldCECompat.ZMod", "ImpactSomethingCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_ZMod_ImpactSomethingCallback
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static bool ImpactSomethingCallback(ProjectileCE projectile, Thing launcher)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
        }
    }
}