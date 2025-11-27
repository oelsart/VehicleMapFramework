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
    private static readonly FastInvokeHandler NeedMealSource = MethodInvoker.GetHandler(AccessTools.Method(typeof(Alert_NeedMealSource), "NeedMealSource"));

    public static void Postfix(Alert_NeedMealSource __instance, Map map, ref bool __result)
    {
        __result &= VehiclePawnWithMapCache.AllVehiclesOn(map).All(v => (bool)NeedMealSource(__instance, v.VehicleMap));
    }
}

[HarmonyPatch(typeof(Alert_NeedColonistBeds), nameof(Alert_NeedColonistBeds.AvailableColonistBeds))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Alert_NeedColonistBeds_AvailableColonistBeds
{
    private static readonly List<Building> buildings = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing))
        {
            yield return instruction;

            if (instruction.LoadsField(AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist))))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.Call(typeof(Patch_Alert_NeedColonistBeds_AvailableColonistBeds), nameof(AddBuildings));
            }
        }
    }

    private static List<Building> AddBuildings(List<Building> list, Map map)
    {
        buildings.Clear();
        buildings.AddRange(list);
        buildings.AddRange(VehiclePawnWithMapCache.AllVehiclesOn(map).SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist));
        return buildings;
    }
}
