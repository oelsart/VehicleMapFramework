using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_Vivi
    {
        static Patches_Vivi()
        {
            if (ModsConfig.IsActive("gguake.race.vivi"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_Vivi");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_Vivi")]
    [HarmonyPatch]
    public static class Patch_ArcanePlant_Turret_TryFindNewTarget_Delegate
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes<MethodInfo>(AccessTools.TypeByName("VVRace.ArcanePlant_Turret"),
                t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_Vivi")]
    [HarmonyPatch("VVRace.ArcanePlant_Turret", "TryFindNewTarget")]
    public static class Patch_ArcanePlant_Turret_TryFindNewTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var f_allBuildingsColonist = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_allBuildingsColonist)) + 1;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_ArcanePlant_Turret_TryFindNewTarget), nameof(Patch_ArcanePlant_Turret_TryFindNewTarget.AddBuildingList))
            });
            return codes;
        }

        private static List<Building> AddBuildingList(List<Building> list, Building instance)
        {
            return list.Concat(instance.Map.BaseMapAndVehicleMaps().Except(instance.Map).SelectMany(m => m.listerBuildings.allBuildingsColonist)).ToList();
        }
    }
}
