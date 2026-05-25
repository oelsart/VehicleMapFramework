using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VQEGenerator)]
[HarmonyPatch("VanillaQuestsExpandedTheGenerator.Building_Genetron", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_Genetron_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var codes = new CodeMatcher(instructions, generator);
    var l_zero = (LocalBuilder)codes.Instructions()
      .Last(c => c.IsLdloc() && (c.operand as LocalBuilder)?.LocalType == typeof(Vector3)).operand;
    return codes
      .MatchStartForward(new CodeMatch(OpCodes.Ldloc_S, l_zero))
      .CreateLabelWithOffsets(1, out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .DeclareLocal(typeof(float), out var rotation)
      .InsertAfterAndAdvance(
        CodeInstruction.LoadArgument(0),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ExtraAngle),
        new CodeInstruction(OpCodes.Stloc_S, rotation),
        new CodeInstruction(OpCodes.Ldloc_S, rotation),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotatedBy))
      .MatchStartForward(CodeMatch.LoadsConstant(0f))
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldloc_S, rotation),
        new CodeInstruction(OpCodes.Add))
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.VQEGenerator)]
[HarmonyPatch("VanillaQuestsExpandedTheGenerator.Building_GenetronOverdrive", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_GenetronOverdrive_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Vector3), "op_Addition", [typeof(Vector3), typeof(Vector3)])))
      .CreateLabel(out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .DeclareLocal(typeof(float), out var rotation)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ExtraAngle),
        new CodeInstruction(OpCodes.Stloc_S, rotation),
        new CodeInstruction(OpCodes.Ldloc_S, rotation),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotatedBy))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity))
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldloc_S, rotation),
        new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.up))),
        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Quaternion), nameof(Quaternion.AngleAxis))),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply))
      .InstructionEnumeration();
  }
}
