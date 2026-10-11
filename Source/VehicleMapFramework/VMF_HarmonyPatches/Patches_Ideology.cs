using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_Ideology
{
  static Patches_Ideology()
  {
    if (ModsConfig.IdeologyActive)
    {
      VMF_Harmony.PatchCategory(PatchCategories.Ideology);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.Ideology)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_IdeoBuildingPresenceDemand
{
  private static  IEnumerable<MethodBase> TargetMethods()
  {
    var type = typeof(IdeoBuildingPresenceDemand);
    yield return AccessTools.Method(type, nameof(IdeoBuildingPresenceDemand.BuildingPresent));
    yield return AccessTools.Method(type, nameof(IdeoBuildingPresenceDemand.RequirementsSatisfied));
    yield return AccessTools.Method(type, nameof(IdeoBuildingPresenceDemand.BestBuilding));
    yield return AccessTools.FindIncludingInnerTypes(type,
      t => !t.Name.Contains("<AllBuildings>")
        ? null
        : t.GetDeclaredMethods().FirstOrDefault(m => m.Name == nameof(IEnumerator.MoveNext)));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MakeThingsOfDefCrossMap()
      .InstructionEnumeration();
  }
}