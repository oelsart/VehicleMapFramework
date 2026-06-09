using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_HospitalityCasino
{
  static Patches_HospitalityCasino()
  {
    if (HospitalityCasino)
    {
      VMF_Harmony.PatchCategory(PatchCategories.HospitalityCasino);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.HospitalityCasino)]
[HarmonyPatch("HospitalityCasino.JobGiver_PlaySlotMachines", "TryGiveJob")]
public static class Patch_JobGiver_PlaySlotMachines_TryGiveJob
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.AddAllBuildingsColonistForThingInstance(1);
  }
}

[HarmonyPatchCategory(PatchCategories.HospitalityCasino)]
[HarmonyPatch]
public static class Patch_JobGiver_PlaySlotMachines_TryGiveJob_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(
      GenTypes.GetTypeInAnyAssembly("HospitalityCasino.JobGiver_PlaySlotMachines", "HospitalityCasino"),
      t => t.GetDeclaredMethods()
        .FirstOrDefault(m => m.Name.Contains("<TryGiveJob>") && m.CallsMethod(CachedMethodInfo.g_Thing_Position)));
  }
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}