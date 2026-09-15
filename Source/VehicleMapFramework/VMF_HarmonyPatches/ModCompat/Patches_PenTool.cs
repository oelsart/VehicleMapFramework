using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_PenTool
{
  static Patches_PenTool()
  {
    if (PenTool)
    {
      VMF_Harmony.PatchCategory(PatchCategories.PenTool);
    }
  }
}

// PenSessionがフォーカスされた車両マップで作成されるように
[HarmonyPatchCategory(PatchCategories.PenTool)]
[HarmonyPatch("PenTool.PenRuntime", "Ensure")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PenRuntime_Ensure
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
  }
}

[HarmonyPatchCategory(PatchCategories.PenTool)]
[HarmonyPatch("PenTool.PenSession", "MousePoint")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PenSession_MousePoint
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(UI), nameof(UI.MouseMapPosition))))
      .InsertAfter(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(GenTypes.GetTypeInAnyAssembly("PenTool.PenSession", "PenTool"), "Map"),
        ((Delegate)ToVehicleMapCoord).Method.CallInstruction)
      .InstructionEnumeration();
  }

  private static Vector3 ToVehicleMapCoord(Vector3 original, Map map)
  {
    return map.IsNonFocusedVehicleMapOf(out var vehicle) ? original.ToVehicleMapCoord(vehicle) : original;
  }
}

// CurrentViewRectのオフセットによるカリング回避とGUI.matrixの変形
[HarmonyPatchCategory(PatchCategories.PenTool)]
[HarmonyPatch("PenTool.PenSession", "Draw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PenSession_Draw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .DeclareLocal(typeof(Matrix4x4), out var matrix)
      .MatchStartForward(
        CodeMatch.Calls(CachedMethodInfo.m_CurrentViewRect))
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldloca_S, matrix),
        ((Delegate)Transform).Method.CallInstruction)
      .MatchStartForward(
        CodeMatch.LoadsField(AccessTools.Field("PenTool.PenSession:drag")))
      .Insert(
        new CodeInstruction(OpCodes.Ldloc_S, matrix),
        AccessTools.PropertySetter(typeof(GUI), nameof(GUI.matrix)).CallInstruction)
      .InstructionEnumeration();
  }

  private static CellRect Transform(CellRect currentViewRect, out Matrix4x4 matrix)
  {
    matrix = GUI.matrix;
    if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle))
    {
      var angle = vehicle.FullAngle;
      var origin = Vector3.zero.ToBaseMapCoord(vehicle).RotatedBy(-angle);
      UI.RotateAroundPivot(vehicle.FullAngle, Vector3.zero.MapToUIPosition());
      GUI.matrix *= Matrix4x4.Translate(origin.MirrorVertical().ToVector2() * UI.CurUICellSize());
      return CellRect.FromLimits(
        currentViewRect.Min.ToVehicleMapCoord(vehicle),
        currentViewRect.Max.ToVehicleMapCoord(vehicle));
    }
    return currentViewRect;
  }
}

// DrawCurveのカリング無効化
[HarmonyPatchCategory(PatchCategories.PenTool)]
[HarmonyPatch("PenTool.PenSession", "DrawCurve")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PenSession_DrawCurve
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var matcher = new CodeMatcher(instructions, generator)
      .MatchStartForward(new CodeMatch(OpCodes.Ret))
      .CreateLabelWithOffsets(1, out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
    return matcher
      .Insert(
        new CodeInstruction(OpCodes.Ldloca_S, vehicle).MoveLabelsFrom(matcher.Instruction),
        ((Delegate)VehicleMapUtility.FocusedOnVehicleMap).Method.CallInstruction,
        new CodeInstruction(OpCodes.Brtrue_S, label))
      .InstructionEnumeration();
  }
}

// CurrentViewRectのオフセットによるカリング回避
[HarmonyPatchCategory(PatchCategories.PenTool)]
[HarmonyPatch("PenTool.PenSession", "DrawBatchGhosts")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PenSession_DrawBatchGhosts
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_CurrentViewRect, ((Delegate)CurrentViewRect).Method);
  }

  private static CellRect CurrentViewRect(CameraDriver driver)
  {
    var currentViewRect = driver.CurrentViewRect;
    if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle))
    {
      return CellRect.FromLimits(
        currentViewRect.Min.ToVehicleMapCoord(vehicle),
        currentViewRect.Max.ToVehicleMapCoord(vehicle));
    }
    return currentViewRect;
  }
}

// Graphic_Linkedの時のBlueprint描画の回転と接続判定の回転
[HarmonyPatchCategory(PatchCategories.PenTool)]
[HarmonyPatch("PenTool.PenSession", "DrawBlueprintCell")]
[PatchLevel(Level.Sensitive)]
public static class Patch_PenSession_DrawBlueprintCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(new CodeMatch(OpCodes.Ldelem))
      .CreateLabel(out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(GenTypes.GetTypeInAnyAssembly("PenTool.PenSession", "PenTool"), "Map"),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        ((Delegate)RotateIndex).Method.CallInstruction)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity))
      .Advance()
      .AddExtraAngle(vehicle)
      .InstructionEnumeration();
  }

  private static int RotateIndex(int i, VehiclePawnWithMap vehicle) =>
    GenMath.PositiveMod(i - vehicle.FullRotation.RotForVehicleDraw().AsInt, 4);
}