using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.ModCompat.VanillaTemperatureExpanded;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VTE)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_VTEPlaceWorkers_DrawGhost
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method("VanillaTemperatureExpanded.Placeworker_AcUnit:DrawGhost");
    yield return AccessTools.Method("VanillaTemperatureExpanded.PlaceWorker_HeaterWithOffset:DrawGhost");
    yield return AccessTools.Method("VanillaTemperatureExpanded.PlaceWorker_TwoCellCooler:DrawGhost");
    yield return AccessTools.Method("VanillaTemperatureExpanded.PlaceWorker_TwoCellHeater:DrawGhost");
  }

  // Find.CurrentMap -> thing?MapHeld ?? VehicleMapUtility.CurrentMap
  // GenDraw.DrawFieldEdges -> GenDrawOnVehicle.DrawFieldEdges
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .SetOperandAndAdvance(CachedMethodInfo.g_VehicleMapUtility_CurrentMap)
      .CreateLabel(out var label)
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(5),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        CodeInstruction.LoadArgument(5),
        new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_MapHeld),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Pop),
        CodeInstruction.LoadArgument(5),
        new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_MapHeld))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenDraw_DrawFieldEdges2))
      .Repeat(c => c
        .InsertAndAdvance(CodeInstruction.LoadLocal(0))
        .Operand = CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges2)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.VTE)]
[HarmonyPatch("ProxyHeat.CompTemperatureSource", "PostDrawExtraSelectionOverlays")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompTemperatureSource_PostDrawExtraSelectionOverlays
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenDraw_DrawFieldEdges2))
      .Repeat(c => c
        .InsertAndAdvance(
          CodeInstruction.LoadArgument(0),
          CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)),
          new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_MapHeld))
        .SetOperandAndAdvance(CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges2))
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.VTE)]
[HarmonyPatch("ProxyHeat.CompTemperatureSource", "TempTick")]
[PatchLevel(Level.Safe)]
public static class Patch_CompTemperatureSource_TempTick
{
  public static void Prefix(ThingComp __instance, ref Map ___map, ref IntVec3 ___position, ref MapComponent ___proxyHeatManager, ref bool ___dirty)
  {
    if (__instance.parent.IsHashIntervalTick(60) && __instance.parent.IsOnVehicleMapOf(out var vehicle))
    {
      var flag = vehicle.Spawned && __instance.parent.Position.UsesOutdoorTemperature(__instance.parent.Map);
      var map = flag ? vehicle.Map : __instance.parent.Map;

      if (map != ___map)
      {
        RemoveComp(___proxyHeatManager, __instance);
        ___map = map;
        ___proxyHeatManager = map.GetComponent(ProxyHeatManager);
        ___dirty = true;
      }

      var pos = flag ? __instance.parent.PositionOnBaseMap : __instance.parent.Position;
      if (pos != ___position)
      {
        ___position = pos;
        ___dirty = true;
      }
    }
  }
}

[HarmonyPatchCategory(PatchCategories.VTE)]
[HarmonyPatch("ProxyHeat.CompTemperatureSource", "GetCells")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompTemperatureSource_GetCells
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_ConditionalBaseMapOccupiedRect = AccessTools.Method(typeof(Patch_CompTemperatureSource_GetCells), nameof(ConditionalBaseMapOccupiedRect));
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_OccupiedRect))
      .Repeat(c => c
        .InsertAndAdvance(
          CodeInstruction.LoadArgument(0),
          new CodeInstruction(OpCodes.Ldfld, AccessTools.Field("ProxyHeat.CompTemperatureSource:map")))
        .Operand = m_ConditionalBaseMapOccupiedRect)
      .InstructionEnumeration();
  }

  private static CellRect ConditionalBaseMapOccupiedRect(Thing t, Map map)
  {
    if (t.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned && vehicle.Map == map &&
        t.Position.UsesOutdoorTemperature(t.Map))
    {
      return t.MovedOccupiedRect();
    }
    return t.OccupiedRect();
  }
}
