﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patches_AbilityComp
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in typeof(AbilityComp).AllSubclasses().Concat(typeof(GenClamor)))
        {
            foreach (var method in type.GetDeclaredMethods())
            {
                if (VMF_Harmony.ReadMethodBodyWrapper(method).Any(i =>
                {
                    return CachedMethodInfo.g_Thing_Position.Equals(i.Value) ||
                    CachedMethodInfo.g_LocalTargetInfo_Cell.Equals(i.Value) ||
                    CachedMethodInfo.g_Thing_Map.Equals(i.Value) ||
                    CachedMethodInfo.g_Thing_MapHeld.Equals(i.Value) ||
                    CachedMethodInfo.m_OccupiedRect.Equals(i.Value) ||
                    CachedMethodInfo.m_BreadthFirstTraverse.Equals(i.Value);
                }))
                {
                    yield return method;
                }
            }
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap)
            .MethodReplacer(CachedMethodInfo.m_BreadthFirstTraverse, CachedMethodInfo.m_BreadthFirstTraverseAcrossMaps);
    }
}
