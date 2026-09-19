using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Alert_NeedMealSource), "NeedMealSource")]
[PatchLevel(Level.Safe)]
public static class Patch_Alert_NeedMealSource_NeedMealSource
{
  private static readonly FastInvokeHandler NeedMealSource =
    MethodInvoker.GetHandler(AccessTools.Method(typeof(Alert_NeedMealSource), "NeedMealSource"));

  public static void Postfix(Alert_NeedMealSource __instance, Map map, ref bool __result)
  {
    if (!__result) return;

    foreach (var map2 in map.BaseMapAndVehicleMaps(false))
    {
      if (!(bool)NeedMealSource(__instance, SingleParam.Get(map2)))
      {
        __result = false;
        return;
      }
    }
  }
}

[HarmonyPatch(typeof(Alert_NeedColonistBeds), nameof(Alert_NeedColonistBeds.AvailableColonistBeds))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Alert_NeedColonistBeds_AvailableColonistBeds
{
  private static readonly List<Building> buildings = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var f_allBuildingsColonist =
      AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
    
    foreach (var instruction in instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map,
               CachedMethodInfo.m_BaseMapOrCaravan_Thing))
    {
      yield return instruction;

      if (instruction.LoadsField(f_allBuildingsColonist))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return ((Delegate)AddBuildings).Method.CallInstruction;
      }
    }
  }

  private static List<Building> AddBuildings(List<Building> list, Map map)
  {
    var baseMapAndVehicleMaps = map.BaseMapAndVehicleMaps(false);
    if (baseMapAndVehicleMaps.NullOrEmpty()) return list;

    buildings.Clear();
    buildings.AddRange(list);
    buildings.AddRange(baseMapAndVehicleMaps.SelectMany(m => m.listerBuildings.allBuildingsColonist));
    return buildings;
  }
}