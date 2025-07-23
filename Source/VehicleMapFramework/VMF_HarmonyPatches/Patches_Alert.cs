using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Alert_NeedMealSource), "NeedMealSource")]
public static class Patch_Alert_NeedMealSource_NeedMealSource
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(Alert_NeedMealSource __instance, Map map, ref bool __result)
    {
        __result &= VehiclePawnWithMapCache.AllVehiclesOn(map).All(v => (bool)NeedMealSource(__instance, v.VehicleMap));
    }

    private static FastInvokeHandler NeedMealSource = MethodInvoker.GetHandler(AccessTools.Method(typeof(Alert_NeedMealSource), "NeedMealSource"));
}

[HarmonyPatch(typeof(Alert_NeedColonistBeds), nameof(Alert_NeedColonistBeds.AvailableColonistBeds))]
public static class Patch_Alert_NeedColonistBeds_AvailableColonistBeds
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
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

    private static List<Building> buildings = [];
}
