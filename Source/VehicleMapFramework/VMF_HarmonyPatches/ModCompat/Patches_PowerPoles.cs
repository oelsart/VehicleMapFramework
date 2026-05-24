using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat.PowerPoles;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_PowerPoles
{
    static Patches_PowerPoles()
    {
        if (Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.PowerPoles);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch("RimForge.Buildings.Building_LongDistancePower", "CanLinkTo")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_LongDistancePower_CanLinkTo
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }

    public static void Postfix(Building __instance, Building other, ref bool __result)
    {
        __result &= __instance.Map == other.Map ||
                    (__instance.GetComp<CompPowerPole>(), other.GetComp<CompPowerPole>()) is ({ } comp, { } comp2) &&
                    comp.CanLinkTo(comp2);
    }
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch("RimForge.Buildings.Building_LongDistancePower", "TryAddLink")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_LongDistancePower_TryAddLink
{
    public static void Postfix(Building __instance, Building item, bool __result)
    {
        if (__result && __instance.Map != item.Map &&
            (__instance.GetComp<CompPowerPole>(), item.GetComp<CompPowerPole>()) is ({ } comp, { } comp2))
        {
            comp.Connect(comp2);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch("RimForge.Buildings.Building_LongDistancePower", "TryRemoveLink")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_LongDistancePower_TryRemoveLink
{
    public static void Postfix(Building __instance, Building item, bool __result)
    {
        if (__result && (__instance.GetComp<CompPowerPole>(), item.GetComp<CompPowerPole>()) is ({ } comp, { } comp2) &&
            comp.LinkedComp == comp2)
        {
            comp.Disconnect();
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch("RimForge.Buildings.Building_LongDistancePower", "GetAllLinked")]
public static class Patch_Building_LongDistancePower_GetAllLinked
{
    [PatchLevel(Level.Safe)]
    public static IEnumerable<Building> Postfix(IEnumerable<Building> values, Building __instance)
    {
        foreach (var building in values)
        {
            if (__instance.Map == building.Map)
            {
                yield return building;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    [PatchLevel(Level.Mandatory)]
    public static IEnumerable<Building> GetAllLinked(Building instance, bool sanitize) => throw new NotImplementedException();
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch("RimForge.Buildings.Building_LongDistancePower", "DisconnectAll")]
public static class Patch_Building_LongDistancePower_DisconnectAll
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        const string GetAllLinked = nameof(Patch_Building_LongDistancePower_GetAllLinked.GetAllLinked);
        return instructions.MethodReplacer(
            AccessTools.Method(Building_LongDistancePower, GetAllLinked),
            AccessTools.Method(typeof(Patch_Building_LongDistancePower_GetAllLinked), GetAllLinked));
    }
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPowerPole_GetFlatConnectionPoint
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return Building_LongDistanceCabled.AllSubclasses()
            .Select(t => AccessTools.DeclaredMethod(t, "GetFlatConnectionPoint"))
            .Where(m => m != null);
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotation);
    }

    public static void Postfix(Thing __instance, ref Vector2 __result)
    {
        if (__instance.IsOnVehicleMapOf(out var vehicle))
        {
            __result = Ext_Math.RotatePoint(__result.ToVector3(), __instance.DrawPos, -vehicle.ExtraAngle).ToVector2();
        }
    }
}

[HarmonyPatchCategory(PatchCategories.PowerPoles)]
[HarmonyPatch("RimForge.Buildings.Building_LongDistancePower", "Power", MethodType.Getter)]
[PatchLevel(Level.Mandatory)]
public static class Patch_Building_LongDistancePower_Power
{
    public static void Postfix(ThingWithComps __instance, ref CompPower __result)
    {
        __result ??= __instance.GetComp<CompPowerPole>();
    }
}