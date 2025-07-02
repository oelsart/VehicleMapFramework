using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_AllowTool
{
    public delegate Job TryGetJobOnThing(Pawn pawn, Thing t, bool forced);

    public static TryGetJobOnThing JobOnThingDelegate;

    static Patches_AllowTool()
    {
        if (ModCompat.AllowTool)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_AllowTool");

            JobOnThingDelegate = HaulAIAcrossMapsUtility.HaulToStorageJobReplace;
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch("AllowTool.WorkGiver_HaulUrgently", "JobOnThing")]
public static class Patch_WorkGiver_HaulUrgently_JobOnThing
{
    public static bool Prefix(Pawn pawn, Thing t, bool forced, ref Job __result)
    {
        __result = Patches_AllowTool.JobOnThingDelegate(pawn, t, forced);
        return false;
    }
}

[HarmonyPatchCategory("VMF_Patches_AllowTool")]
[HarmonyPatch("AllowTool.WorkGiver_HaulUrgently", "PotentialWorkThingsGlobal")]
public static class Patch_WorkGiver_HaulUrgently_PotentialWorkThingsGlobal
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_PawnCanAutomaticallyHaulFast, CachedMethodInfo.m_PawnCanAutomaticallyHaulFastReplace);
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