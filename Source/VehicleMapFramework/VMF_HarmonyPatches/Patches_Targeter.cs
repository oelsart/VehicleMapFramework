﻿using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Targeter), "ConfirmStillValid")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_ConfirmStillValid
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
    }
}

[HarmonyPatch(typeof(Targeter), "OrderVerbForceTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_OrderVerbForceTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(Targeter), "CurrentTargetUnderMouse")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_CurrentTargetUnderMouse
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_GenUI_TargetsAtMouse, CachedMethodInfo.m_GenUIOnVehicle_TargetsAtMouse);
    }
}