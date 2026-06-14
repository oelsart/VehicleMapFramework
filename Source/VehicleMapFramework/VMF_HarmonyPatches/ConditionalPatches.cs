using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[AttributeUsage(AttributeTargets.Class)]
internal class VfVersionalPatchAttribute : Attribute
{
  internal const string LatestRelease = "1.6.2144";
  internal const string CurrentDevBranch = "1.6.2361";

  public VfVersionalPatchAttribute(string version, ComparisonType comparison = ComparisonType.Equal)
  {
    TargetVersion = Version.Parse(version);
    Available = comparison switch
    {
      ComparisonType.LessThan => CurrentVersion < TargetVersion,
      ComparisonType.LessThanOrEqual => CurrentVersion <= TargetVersion,
      ComparisonType.Equal => CurrentVersion == TargetVersion,
      ComparisonType.GreaterThan => CurrentVersion > TargetVersion,
      ComparisonType.GreaterThanOrEqual => CurrentVersion >= TargetVersion,
      ComparisonType.NotEqual => CurrentVersion != TargetVersion,
      _ => false
    };
  }

  public bool Available { get; }

  private Version TargetVersion { get; }

  private static Version CurrentVersion { get; } =
    Version.Parse(Regex.Replace(VehicleMod.metaData?.ModVersion ?? LatestRelease, @"[^\d.]", ""));
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehiclePath), nameof(VehiclePath.DrawPath))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehiclePath_DrawPath
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return Patch_PawnPath_DrawPath.Transpiler(instructions, generator);
  }
}

[VfVersionalPatch(VfVersionalPatchAttribute.LatestRelease, ComparisonType.LessThanOrEqual)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.DrawGhostVehicleDef))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostVehicleDef
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenThing_TrueCenter2));
    codes.InsertAfter(
      CodeInstruction.LoadArgument(5),
      ((Delegate)ToTargetMapCoord).Method.CallInstruction);
    return codes.Instructions();
  }

  public static Vector3 ToTargetMapCoord(Vector3 original, Thing thing)
  {
    return thing.TryGetTargetMap(out var map) ? original.ToBaseMapCoord(map).WithY(original.y) : original;
  }
}

[VfVersionalPatch(VfVersionalPatchAttribute.LatestRelease, ComparisonType.LessThanOrEqual)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.DrawGhostOverlays))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostOverlays
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenThing_TrueCenter2));
    codes.InsertAfter(
      CodeInstruction.LoadArgument(6),
      ((Delegate)Patch_VehicleGhostUtility_DrawGhostVehicleDef.ToTargetMapCoord).Method.CallInstruction);
    return codes.Instructions();
  }
}

[VfVersionalPatch(VfVersionalPatchAttribute.LatestRelease, ComparisonType.GreaterThan)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch("Vehicles.VehicleGhostUtility+DrawData", "DrawPos", MethodType.Getter)]
public static class Patch_VehicleGhostUtility_DrawData_DrawPos
{
  [PatchLevel(Level.Sensitive)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var type = GenTypes.GetTypeInAnyAssembly("Vehicles.VehicleGhostUtility+DrawData");
    var f_rot = AccessTools.Field(type, "rot");
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.LoadsField(f_rot))
      .InsertAfterAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(type, "vehicle"),
        ((Func<Rot8, VehiclePawn, Rot8>)BaseRot).Method.CallInstruction)
      .MatchStartForward(CodeMatch.LoadsField(f_rot))
      .InsertAfter(((Func<Rot8, Rot8>)FocusedRot).Method.CallInstruction)
      .InstructionEnumeration();

    Rot8 BaseRot(Rot8 rot, VehiclePawn vehicle)
    {
      if (Command_FocusVehicleMap.FocusedVehicle is { } vehicle2)
      {
        return rot.Rotated(vehicle2.FullRotation);
      }
      if (vehicle.TryGetTargetMap(out var map) && map.IsVehicleMapOf(out var vehicle3))
      {
        return rot.Rotated(vehicle3.FullRotation);
      }
      return rot;
    }

    Rot8 FocusedRot(Rot8 rot)
    {
      if (Command_FocusVehicleMap.FocusedVehicle is { } vehicle)
      {
        return rot.Rotated(vehicle.FullRotation);
      }
      return rot;
    }
  }
  
  [PatchLevel(Level.Safe)]
  public static void Postfix(VehiclePawn ___vehicle, ref Vector3 __result)
  {
    if (___vehicle is not null)
    {
      __result = ___vehicle.TryGetTargetMap(out var map) ? __result.ToBaseMapCoord(map).WithY(__result.y) : __result;
      return;
    }
    if (Command_FocusVehicleMap.FocusedVehicle is null &&
      UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
    {
      __result = __result.ToBaseMapCoord(vehicle);
    }
  }
}

[VfVersionalPatch(VfVersionalPatchAttribute.LatestRelease, ComparisonType.LessThanOrEqual)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TransferableVehicleWidget), "DrawCard")]
[PatchLevel(Level.Safe)]
public static class Patch_TransferableVehicleWidget_DrawCard
{
  internal static VehiclePawnWithMap vehicle;

  public static void Prefix(TransferableOneWay transferable)
  {
    if (Event.current.type == EventType.Repaint)
      vehicle = transferable.AnyThing as VehiclePawnWithMap;
  }
}

[VfVersionalPatch(VfVersionalPatchAttribute.LatestRelease, ComparisonType.LessThanOrEqual)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TextureDrawer), "Draw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TextureDrawer_Draw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(
        AccessTools.Method(typeof(UIElements), nameof(UIElements.DrawTextureWithMaterialOnGUI))))
      .InsertAfter(
        CodeInstruction.LoadLocal(3),
        ((Delegate)TryRenderVehicleMap).Method.CallInstruction)
      .InstructionEnumeration();
  }

  public static void TryRenderVehicleMap(Rect drawRect)
  {
    ref var vehicle = ref Patch_TransferableVehicleWidget_DrawCard.vehicle;
    if (vehicle is not null)
    {
      Vector2? drawSize = null;
      Vector3? drawOffset = null;
      if (!vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
      {
        drawSize = vehicle.DrawSize;
        drawOffset = VehicleMapUtility.OffsetFor(vehicle, Rot4.East);
      }
      var texture = VehicleMapUIRenderer.GetVehicleMapTexture(vehicle,
        Rot4.East,
        new Vector2Int(256, 256),
        drawSize,
        drawOffset);
      var rect2 = new Rect(0f, 0f, 150f, 150f)
      {
        center = drawRect.center
      };
      Widgets.DrawTextureFitted(rect2, texture, 1f);

      vehicle = null;
    }
  }
}

[VfVersionalPatch("1.6.2380", ComparisonType.GreaterThanOrEqual)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(BlitRequest), nameof(BlitRequest.For), typeof(VehiclePawn))]
[PatchLevel(Level.Safe)]
public static class Patch_BlitRequest_For
{
  public static void Postfix(VehiclePawn vehicle, ref BlitRequest __result)
  {
    if (vehicle is VehiclePawnWithMap vehiclePawnWithMap)
      __result.blitTargets.Add(vehiclePawnWithMap.VehicleMapBlitter);
  }
}