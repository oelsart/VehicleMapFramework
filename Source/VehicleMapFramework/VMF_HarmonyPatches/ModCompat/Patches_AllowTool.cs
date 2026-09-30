using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AllowTool
{
  static Patches_AllowTool()
  {
    if (AllowTool)
    {
      VMF_Harmony.PatchCategory(PatchCategories.AllowTool);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch("AllowTool.UnlimitedAreaDragger", "Update")]
[PatchLevel(Level.Safe)]
public static class Patch_UnlimitedAreaDragger_Update
{
  internal static void Prefix([MustDisposeResource] ref FocusMapScope __state)
  {
    if (Command_FocusVehicleMap.FocusedVehicle is { } focused)
      __state = FocusMapScope.FocusMapUnsafe(focused.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatchCategory(PatchCategories.AllowTool)]
[HarmonyPatch("AllowTool.MapCellHighlighter+CachedHighlight", null, MethodType.Constructor)]
[HarmonyPatch([typeof(Vector3), typeof(Material)])]
[PatchLevel(Level.Safe)]
public static class Patch_MapCellHighlighter_CachedHighlight
{
  public static void Prefix(ref Vector3 drawPosition)
  {
    drawPosition = drawPosition.ToBaseMapCoord().WithY(drawPosition.y);
  }
}
