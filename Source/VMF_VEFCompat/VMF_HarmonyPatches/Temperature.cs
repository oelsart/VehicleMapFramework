using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.ModCompat.VanillaTemperatureExpanded;

namespace VehicleMapFramework.VMF_HarmonyPatches;

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
    var m_ConditionalBaseMapOccupiedRect = ((Delegate)ConditionalBaseMapOccupiedRect).Method;
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
