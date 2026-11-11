using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Targeter), "ConfirmStillValid")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Targeter_ConfirmStillValid
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var code = CachedMethodInfo.m_BaseMapOrCaravan_Map.CallInstruction;
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .Repeat(c =>
      {
        c.InsertAndAdvance(code);
        c.InsertAfterAndAdvance(code).Advance();
      }).InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(Targeter), "OrderVerbForceTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_OrderVerbForceTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_Targeter_ConfirmStillValid.Transpiler(instructions);
  }
}

[HarmonyPatch(typeof(Targeter), "CurrentTargetUnderMouse")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_CurrentTargetUnderMouse
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_GenUI_TargetsAtMouse,
      CachedMethodInfo.m_GenUIOnVehicle_TargetsAtMouse);
  }
}

[HarmonyPatch(typeof(Targeter), nameof(Targeter.TargeterUpdate))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Targeter_TargeterUpdate
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Targeter), "CurrentTargetUnderMouse")))
      .Advance()
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .DeclareLocal(typeof(Thing), out var caster)
      .CreateLabel(out var label)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Targeter), nameof(Targeter.targetingSource)),
        AccessTools.PropertyGetter(typeof(ITargetingSource), nameof(ITargetingSource.Caster)).CallvirtInstruction,
        new CodeInstruction(OpCodes.Stloc_S, caster),
        new CodeInstruction(OpCodes.Ldloc_S, caster),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, caster),
        ((Delegate)TargetMapUtility.get_TargetMap).Method.CallInstruction,
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        CachedMethodInfo.m_IsNonFocusedVehicleMapOf.CallInstruction,
        new CodeInstruction(OpCodes.Pop))
      .FocusVehicleAroundMethod(vehicle,
        AccessTools.Method(typeof(ITargetingSource), nameof(ITargetingSource.DrawHighlight)))
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Targeter), "highlightAction")))
      .FocusVehicleAroundMethod(vehicle, AccessTools.Method(typeof(Action<LocalTargetInfo>), nameof(Action.Invoke)))
      .InstructionEnumeration();
  }
}