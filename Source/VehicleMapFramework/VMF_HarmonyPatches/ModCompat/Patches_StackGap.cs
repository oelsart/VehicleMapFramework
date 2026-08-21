using System;
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
    codes.MatchStartForward(CodeMatch.Calls(((Func<IntVec3, Map, SlotGroup>)StoreUtility.GetSlotGroup).Method));
    codes.MatchStartBackwards(new CodeMatch(OpCodes.Ldarg_2));
    codes.Insert(
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadArgument(1),
      new CodeInstruction(OpCodes.Call, ((Delegate)TryReplaceMap).Method));
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
