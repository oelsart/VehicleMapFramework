using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_SmarterConstruction
    {
        static Patches_SmarterConstruction()
        {
            if (ModsConfig.IsActive("dhultgren.smarterconstruction"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_SmarterConstruction");
                var original = AccessTools.Method(typeof(WorkGiver_ConstructFinishFramesAcrossMaps), nameof(WorkGiver_ConstructFinishFramesAcrossMaps.JobOnThing));
                var postfix = AccessTools.Method("SmarterConstruction.Patches.WorkGiver_ConstructFinishFrames_JobOnThing:Postfix");
                VMF_Harmony.Instance.Patch(original, postfix: postfix);
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
            codes[pos].operand = MethodInfoCache.m_CellOnBaseMap_TargetInfo;
            codes[pos - 2].opcode = OpCodes.Call;
            codes[pos - 2].operand = MethodInfoCache.m_PositionOnBaseMap;
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_SmarterConstruction")]
    [HarmonyPatch("SmarterConstruction.Patches.CustomGenClosest", "ClosestThing_Global_Reachable_Custom")]
    public static class Patch_CustomGenClosest_ClosestThing_Global_Reachable_Custom
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }
}
