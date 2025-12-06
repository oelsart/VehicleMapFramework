using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_StackGap
{
    static Patches_StackGap()
    {
        if (StackGap.Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.StackGap);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.StackGap)]
[HarmonyPatch("StorageUpperBound.HaulingUtility", "TryGetHaulingDestination")]
[PatchLevel(Level.Sensitive)]
public static class Patch_HaulingUtility_TryGetHaulingDestination
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.GetSlotGroup), [typeof(IntVec3), typeof(Map)])));
        codes.MatchStartBackwards(new CodeMatch(OpCodes.Ldarg_2));
        codes.Insert(
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadArgument(1),
            CodeInstruction.Call(typeof(Patch_HaulingUtility_TryGetHaulingDestination), nameof(TryReplaceMap)));
        return codes.Instructions();
    }

    private static void TryReplaceMap(Job job, ref Map map)
    {
        var map2 = job?.globalTarget.Map;
        if (map2 != null)
        {
            map = map2;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.StackGap)]
[HarmonyPatch("StorageUpperBound.ToilsRecipePatch", "PatchNum")]
[PatchLevel(Level.Cautious)]
public static class Patch_ToilsRecipePatch_PatchNum
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}