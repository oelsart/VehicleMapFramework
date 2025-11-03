using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_PRF
{
    static Patches_PRF()
    {
        if (ModCompat.ProjectRimFactory)
        {
            VMF_Harmony.PatchCategory(PatchCategories.ProjectRimFactory);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.ProjectRimFactory)]
[HarmonyPatch("ProjectRimFactory.Common.HarmonyPatches.Patch_CanReserve_SAL", "Postfix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_CanReserve_SAL_Postfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map)) - 1;
        codes[pos].opcode = OpCodes.Ldloc_2;
        return codes;
    }
}

[HarmonyPatchCategory(PatchCategories.ProjectRimFactory)]
[HarmonyPatch("ProjectRimFactory.Drones.AI.JobGiver_DroneMain", "TryGiveJob")]
[PatchLevel(Level.Cautious)]
public static class Patch_JobGiver_DroneMain_TryGiveJob
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.ProjectRimFactory)]
[HarmonyPatch("ProjectRimFactory.Drones.AI.JobGiver_DroneFlee", "ReturnToStationJob")]
[PatchLevel(Level.Cautious)]
public static class Patch_JobGiver_DroneFlee_ReturnToStationJob
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}