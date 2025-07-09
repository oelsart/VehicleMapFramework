using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_SmarterConstruction
{
    static Patches_SmarterConstruction()
    {
        if (ModCompat.SmarterConstruction)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_SmarterConstruction");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_SmarterConstruction")]
[HarmonyPatch("SmarterConstruction.Patches.Patch_WorkGiver_Scanner_GetPriority", "PriorityPostfix")]
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

[HarmonyPatchCategory("VMF_Patches_SmarterConstruction")]
[HarmonyPatch("SmarterConstruction.Patches.CustomGenClosest", "ClosestThing_Global_Reachable_Custom")]
public static class Patch_CustomGenClosest_ClosestThing_Global_Reachable_Custom
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_SmarterConstruction")]
[HarmonyPatch("SmarterConstruction.Core.PathGridWrapper", "Walkable")]
public static class Patch_PathGridWrapper_Walkable
{
    public static void Postfix(IntVec3 loc, PathGrid ____pathGrid, ref bool __result)
    {
        if (!__result)
        {
            var map2 = map(____pathGrid);
            var inBounds = loc.InBounds(map2);
            if ((inBounds && loc.GetEdifice(map(____pathGrid)) is VehicleStructure) || !inBounds)
            {
                __result = true;
            }
        }
    }

    private static AccessTools.FieldRef<PathGrid, Map> map = AccessTools.FieldRefAccess<PathGrid, Map>("map");
}
