using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_TextTool
{
  static Patches_TextTool()
  {
    if (TextTool)
    {
      VMF_Harmony.PatchCategory(PatchCategories.TextTool);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.TextTool)]
[HarmonyPatch("TextTool.Designator_TextTool", "DesignateSingleCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Designator_TextTool_DesignateSingleCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      ((Delegate)UI.MouseMapPosition).Method,
      ((Delegate)MouseVehicleMapPosition).Method);
  }

  private static Vector3 MouseVehicleMapPosition()
  {
    return VehicleMapUtility.CurrentMap.IsVehicleMapOf(out var vehicle)
      ? UI.MouseMapPosition().ToVehicleMapCoord(vehicle)
      : UI.MouseMapPosition();
  }
}

// MapText.DrawPosは結局使われていないためパッチを無効化。これが使われていれば下2つのパッチは不要になる
// [HarmonyPatchCategory(PatchCategories.TextTool)]
// [HarmonyPatch("TextTool.MapText", nameof(Thing.DrawPos), MethodType.Getter)]
// [PatchLevel(Level.Safe)]
// public static class Patch_MapText_DrawPos
// {
//   public static void Postfix(Thing __instance, ref Vector3 __result)
//   {
//     if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
//       __result = __result.ToBaseMapCoord(vehicle);
//   }
// }

[HarmonyPatchCategory(PatchCategories.TextTool)]
[HarmonyPatch("TextTool.MapText", "DoOverlayGUI")]
[PatchLevel(Level.Sensitive)]
public static class Patch_MapText_DoOverlayGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field("TextTool.MapText:exactPosition")))
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldarg_0),
        ((Delegate)ToBaseMapCoord).Method.CallInstruction)
      .InstructionEnumeration();
  }

  private static Vector3 ToBaseMapCoord(Vector3 original, Thing thing)
  {
    return thing.IsOnNonFocusedVehicleMapOf(out var vehicle)
      ? original.ToBaseMapCoord(vehicle)
      : original;
  }
}

[HarmonyPatchCategory(PatchCategories.TextTool)]
[HarmonyPatch("TextTool.MapText", "IsNearCurrentView")]
[PatchLevel(Level.Safe)]
public static class Patch_MapText_IsNearCurrentView
{
  public static bool Prefix(Thing __instance, float ___scale, Vector3 ___exactPosition, ref bool __result)
  {
    if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      var viewRect = Find.CameraDriver.CurrentViewRect;
      var margin = Mathf.CeilToInt(Mathf.Max(30f, ___scale * 16f));
      __result = viewRect.ExpandedBy(margin).Contains(___exactPosition.ToBaseMapCoord(vehicle).ToIntVec3());
      return false;
    }

    return true;
  }
}

[HarmonyPatchCategory(PatchCategories.TextTool)]
[HarmonyPatch("TextTool.MapText", "DrawActiveState")]
public static class Patch_MapText_DrawActiveState
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(Thing __instance, ref Command_FocusVehicleMap.FocusVehicle? __state)
  {
    if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
      __state = new Command_FocusVehicleMap.FocusVehicle(vehicle);
  }

  [PatchLevel(Level.Safe)]
  public static void Finalizer(Command_FocusVehicleMap.FocusVehicle? __state) => __state?.Dispose();
  
  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_Designator_TextTool_DesignateSingleCell.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.TextTool)]
[HarmonyPatch("TextTool.MapText", "ApplyTextTransform")]
[PatchLevel(Level.Safe)]
public static class Patch_MapText_ApplyTextTransform
{
  public static void Postfix(Thing __instance, Vector2 screenCenter)
  {
    if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      GUI.matrix *= Matrix4x4.TRS(screenCenter, Quaternion.Euler(0f, 0f, vehicle.FullAngle), Vector3.one)
                    * Matrix4x4.TRS(-screenCenter, Quaternion.identity, Vector3.one);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.TextTool)]
[HarmonyPatch("TextTool.MapComponentUtility_MapComponentOnGUI_Patch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_MapComponentUtility_MapComponentOnGUI_Patch_Postfix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      if (instruction.Calls(CachedMethodInfo.g_Thing_Map))
      {
        yield return CachedMethodInfo.m_BaseMapOrCaravan_Thing.CallInstruction;
      }
      else
      {
        yield return instruction;
        if (instruction.opcode == OpCodes.Ldarg_0)
          yield return CachedMethodInfo.m_BaseMapOrCaravan_Map.CallInstruction;
      }
    }
  }
}