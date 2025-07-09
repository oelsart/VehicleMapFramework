using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_AllowTool
{
    static Patches_AllowTool()
    {
        if (ModCompat.AllowTool)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_AllowTool");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch("AllowTool.Designator_SelectSimilar", "ProcessSingleCellClick")]
public static class Patch_Designator_SelectSimilar_ProcessSingleCellClick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch("AllowTool.Designator_SelectableThings", "DesignateMultiCell")]
public static class Patch_Designator_SelectableThings_DesignateMultiCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch("AllowTool.UnlimitedAreaDragger", "OnSelectionStarted")]
public static class Patch_UnlimitedAreaDragger_OnSelectionStarted
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch("AllowTool.UnlimitedAreaDragger", "Update")]
public static class Patch_UnlimitedAreaDragger_Update
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch]
public static class Patch_MapCellHighlighter_CachedHighlight
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.TypeByName("AllowTool.MapCellHighlighter+CachedHighlight").Constructor([typeof(Vector3), typeof(Material)]);
    }

    public static void Prefix(ref Vector3 drawPosition)
    {
        if (Find.CurrentMap.IsVehicleMapOf(out var vehicle) || (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null)
        {
            drawPosition = drawPosition.ToBaseMapCoord(vehicle).WithY(drawPosition.y);
        }
    }
}