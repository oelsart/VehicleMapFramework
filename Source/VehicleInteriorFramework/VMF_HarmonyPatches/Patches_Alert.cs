using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [HarmonyPatch(typeof(Alert_NeedMealSource), "NeedMealSource")]
    public static class Patch_Alert_NeedMealSource_NeedMealSource
    {
        public static void Postfix(Alert_NeedMealSource __instance, Map map, bool __result)
        {
            __result = __result && VehiclePawnWithMapCache.AllVehiclesOn(map).All(v => (bool)NeedMealSource(__instance, v.VehicleMap));
        }

        private static FastInvokeHandler NeedMealSource = MethodInvoker.GetHandler(AccessTools.Method(typeof(Alert_NeedMealSource), "NeedMealSource"));
    }

    [HarmonyPatch(typeof(Alert_NeedColonistBeds), nameof(Alert_NeedColonistBeds.AvailableColonistBeds))]
    public static class Patch_Alert_NeedColonistBeds_AvailableColonistBeds
    {
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

        private static List<Building> AddBuildings(List<Building> buildings, Map map)
        {
            buildings.Clear();
            buildings.AddRange(buildings);
            buildings.AddRange(VehiclePawnWithMapCache.AllVehiclesOn(map).SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist));
            return buildings;
        }

        private static List<Building> buildings = new List<Building>();
    }

    [HarmonyPatch(typeof(ListerBuildings), "ColonistsHaveResearchBench")]
    public static class Debug
    {
        public static void Postfix(ListerBuildings __instance)
        {
            //Find.Maps.Do(m => Log.Message(m.listerBuildings.allBuildingsColonist.Count));
        }
    }
}
