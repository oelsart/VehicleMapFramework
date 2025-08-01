﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_SmarterConstruction
{
    public const string Category = "VMF_Patches_SmarterConstruction";

    static Patches_SmarterConstruction()
    {
        if (ModCompat.SmarterConstruction)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_SmarterConstruction.Category)]
[HarmonyPatch("SmarterConstruction.Patches.Patch_WorkGiver_Scanner_GetPriority", "PriorityPostfix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_WorkGiver_Scanner_GetPriority_PriorityPostfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var m_DistanceTo = AccessTools.Method(typeof(IntVec3Utility), nameof(IntVec3Utility.DistanceTo));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_DistanceTo)) - 1;
        codes[pos].operand = CachedMethodInfo.m_CellOnBaseMap_TargetInfo;
        codes[pos - 2].opcode = OpCodes.Call;
        codes[pos - 2].operand = CachedMethodInfo.m_PositionOnBaseMap;
        return codes;
    }
}

[HarmonyPatchCategory(Patches_SmarterConstruction.Category)]
[HarmonyPatch("SmarterConstruction.Patches.CustomGenClosest", "ClosestThing_Global_Reachable_Custom")]
[PatchLevel(Level.Cautious)]
public static class Patch_CustomGenClosest_ClosestThing_Global_Reachable_Custom
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_SmarterConstruction.Category)]
[HarmonyPatch("SmarterConstruction.Core.WalkabilityHandler", "Walkable")]
[PatchLevel(Level.Safe)]
public static class Patch_WalkabilityHandler_Walkable
{
    public static void Postfix(IntVec3 loc, Map ___map, ref bool __result)
    {
        if (!__result)
        {
            var inBounds = loc.InBounds(___map);
            if ((inBounds && loc.GetEdifice(___map) is VehicleStructure) || !inBounds)
            {
                __result = true;
            }
        }
    }
}
