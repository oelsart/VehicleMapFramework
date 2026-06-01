using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.Planet;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_ReGrowth
{
  static Patches_ReGrowth()
  {
    if (SmartFarming.ReGrowthActive)
    {
      VMF_Harmony.PatchCategory(PatchCategories.ReGrowth);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.ReGrowth)]
[HarmonyPatch("ReGrowthCore.MapComponent_SmartFarming", "FinalizeInit")]
public static class Patch_MapComponent_SmartFarming_FinalizeInit
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(new CodeMatch(c => c.opcode == OpCodes.Isinst && c.OperandIs(typeof(PocketMapParent))));
    codes.InsertAfter(CodeInstruction.Call(typeof(Patch_MapComponent_SmartFarming_FinalizeInit), nameof(CheckNotVehicleMapParent)));
    return codes.Instructions();
  }

  private static PocketMapParent CheckNotVehicleMapParent(PocketMapParent mapParent)
  {
    return mapParent is MapParent_Vehicle ? null : mapParent;
  }
}
