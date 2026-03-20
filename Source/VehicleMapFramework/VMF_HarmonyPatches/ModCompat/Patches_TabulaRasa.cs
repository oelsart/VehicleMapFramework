using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_TabulaRasa
{
    static Patches_TabulaRasa()
    {
        if (TabulaRasa)
        {
            VMF_Harmony.PatchCategory(PatchCategories.TabulaRasa);
            try
            {
                Patch_Projectile_CheckForFreeInterceptBetween.Postfixes.Add(
                    Patch_Patch_Projectile_CheckForFreeInterceptBetween_Postfix.PostfixPatch);
            }
            catch (Exception ex)
            {
                VMF_Log.Error($"{ex}");
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch("TabulaRasa.Comp_Shield", "CurShieldPosition", MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Comp_Shield_CurShieldPosition
{
    public static void Postfix(ThingWithComps ___parent, ref Vector3 __result)
    {
        if (___parent.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            __result = __result.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch("TabulaRasa.Comp_Shield", "ShouldBeBlocked")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_Shield_ShouldBeBlocked
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch("TabulaRasa.Comp_Shield", "BombardmentCanStartFireAt")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_Shield_BombardmentCanStartFireAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch("TabulaRasa.Patch_Projectile_CheckForFreeInterceptBetween", "Postfix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Patch_Projectile_CheckForFreeInterceptBetween_Postfix
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static void PostfixPatch(Projectile __instance, ref bool __result, Vector3 lastExactPos, Vector3 newExactPos)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch("TabulaRasa.Patch_Skyfaller_Tick", "Prefix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Patch_Skyfaller_Tick_Prefix
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [HarmonyReversePatch]
    public static bool PrefixPatch(Skyfaller __instance)
    {
        _ = Transpiler(null);
        throw new NotImplementedException();
        
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
        }
    }
}

[HarmonyAfter("Neronix17.TabulaRasa.RimWorld")]
[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch(typeof(Skyfaller), "Tick")]
[PatchLevel(Level.Safe)]
public static class Patch_Skyfaller_Tick
{
    public static List<Func<Skyfaller, bool>> Prefixes { get; } = [Patch_Patch_Skyfaller_Tick_Prefix.PrefixPatch];
    
    public static bool Prefix(Skyfaller __instance)
    {
        foreach (var map in __instance.Map.BaseMapAndVehicleMaps(false))
        {
            __instance.TargetMap = map;
            try
            {
                for (var i = 0; i < Prefixes.Count; i++)
                {
                    if (!Prefixes[i](__instance)) return false;
                }
            }
            finally
            {
                __instance.RemoveTargetInfo();
            }
        }
        return true;
    }
}

[HarmonyPatchCategory(PatchCategories.TabulaRasa)]
[HarmonyPatch("TabulaRasa.PlaceWorker_ShowShieldRadius", "DrawGhost")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_ShowShieldRadius_DrawGhost
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Call && instruction.OperandIs(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
            {
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1);
            }
        }
    }
}