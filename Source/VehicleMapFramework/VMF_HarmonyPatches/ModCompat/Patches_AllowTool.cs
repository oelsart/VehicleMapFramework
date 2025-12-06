using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AllowTool
{
    static Patches_AllowTool()
    {
        if (AllowTool)
        {
            VMF_Harmony.PatchCategory(PatchCategories.AllowTool);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch("AllowTool.Designator_SelectSimilar", "ProcessSingleCellClick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Designator_SelectSimilar_ProcessSingleCellClick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch("AllowTool.Designator_SelectableThings", "DesignateMultiCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Designator_SelectableThings_DesignateMultiCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch("AllowTool.UnlimitedAreaDragger", "OnSelectionStarted")]
[PatchLevel(Level.Cautious)]
public static class Patch_UnlimitedAreaDragger_OnSelectionStarted
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch("AllowTool.UnlimitedAreaDragger", "Update")]
[PatchLevel(Level.Cautious)]
public static class Patch_UnlimitedAreaDragger_Update
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch]
[PatchLevel(Level.Safe)]
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