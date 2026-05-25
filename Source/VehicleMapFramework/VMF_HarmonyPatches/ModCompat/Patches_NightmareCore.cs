using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_NightmareCore
{
  static Patches_NightmareCore()
  {
    if (NightmareCore)
    {
      VMF_Harmony.PatchCategory(PatchCategories.NightmareCore);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.NightmareCore)]
[HarmonyPatch("NightmareCore.StitchedAtlasGraphics.Graphic_LinkedStitched", "Print")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Graphic_LinkedStitched_Print
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint).ToList();
    var pos = codes.FindLastIndex(c => c.Calls(AccessTools.Method(typeof(Vector3), "op_Addition")));
    codes.Insert(pos, CodeInstruction.Call(typeof(Patch_Graphic_LinkedStitched_Print), nameof(RotateVector)));

    return codes;
  }

  private static Vector3 RotateVector(Vector3 vector)
  {
    return vector.RotatedBy(VehicleSectionLayerManager.RotForPrintCounter);
  }
}

[HarmonyPatchCategory(PatchCategories.NightmareCore)]
[HarmonyPatch("NightmareCore.ThingComp_AdditionalGraphics", "PostPrintOnto")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ThingComp_AdditionalGraphics_PostPrintOnto
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.LoadsConstant(0f))
      .Set(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation)
      .Insert(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)))
      .InstructionEnumeration();
  }
}
