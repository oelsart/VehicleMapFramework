using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyAfter("LeaveTheMap.Mod")]
[HarmonyPatch(typeof(ExitMapGrid), nameof(ExitMapGrid.MapUsesExitGrid), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_ExitMapGrid_MapUsesExitGrid
{
  public static bool Prepare() => JustLeaveAlready;

  public static void Postfix(Map ___map, ref bool __result) => __result = __result && !___map.IsVehicleMap;
}