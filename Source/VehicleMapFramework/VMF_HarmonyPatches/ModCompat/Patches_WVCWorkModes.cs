using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_WVCWorkModes
{
    static Patches_WVCWorkModes()
    {
        if (WVCWorkModes)
        {
            VMF_Harmony.PatchCategory(PatchCategories.WVCWorkModes);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.WVCWorkModes)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_ShutdownUtility_MechInShutdownZone
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var type = GenTypes.GetTypeInAnyAssembly("WVC_WorkModes.ShutdownUtility", "WVC_WorkModes");
        return AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "MechInShutdownZone");
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_DepartMapOrPawnMap);
    }
}

[HarmonyPatchCategory(PatchCategories.WVCWorkModes)]
[HarmonyPatch("WVC_WorkModes.JobGiver_GoToShutdownZone", "TryGiveJob")]
[PatchLevel(Level.Safe)]
public static class Patch_JobGiver_GoToShutdownZone_TryGiveJob
{
    private static bool working;
    
    public static void Postfix(ThinkNode_JobGiver __instance, Pawn pawn, ref Job __result)
    {
        if (__result is not null || working) return;

        try
        {
            working = true;
            pawn.DepartMap = pawn.Map;
            foreach (var map in pawn.Map.BaseMapAndVehicleMaps.Except(pawn.Map))
            {
                using var _ = new VirtualTeleporter(pawn, map);
                var job = __instance.TryIssueJobPackage(pawn, new JobIssueParams()).Job;
                if (job is not null && pawn.CanReach(job.targetA, PathEndMode.OnCell, Danger.Some, false, false,
                        TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot, out var spotsQueue))
                {
                    __result = JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue, job);
                    break;
                }
            }
            pawn.RemoveDepartMap();
        }
        finally
        {
            working = false;
        }
    }
}