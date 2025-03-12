using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_YayosCombat3
    {
        static Patches_YayosCombat3()
        {
            if (ModsConfig.IsActive("Mlie.YayosCombat3"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_YayosCombat3");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_YayosCombat3")]
    [HarmonyPatch("yayoCombat.HarmonyPatches.Verb_LaunchProjectile_TryCastShot", "Prefix")]
    public static class Patch_Verb_LaunchProjectile_TryCastShot_Prefix
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }
}