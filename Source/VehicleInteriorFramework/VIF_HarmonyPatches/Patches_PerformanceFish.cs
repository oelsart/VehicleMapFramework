using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_PerformanceFish
    {
        static Patches_PerformanceFish()
        {
            if (ModsConfig.IsActive("bs.performance"))
            {
                VIF_Harmony.Instance.PatchCategory("VIF_Patches_PerformanceFish");
            }
        }
    }

    [HarmonyPatchCategory("VIF_Patches_PerformanceFish")]
    [HarmonyPatch]
    public static class Patch_DrawDynamicThingsPatch_CullAndInitializeThings
    {
        private static MethodInfo TargetMethod()
        {
            var type = AccessTools.TypeByName("PerformanceFish.Rendering.DynamicDrawManagerPatches");
            var inner = AccessTools.Inner(type, "DrawDynamicThingsPatch");
            return AccessTools.Method(inner, "CullAndInitializeThings");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_CellRect_ClipInsideMap, MethodInfoCache.m_ClipInsideVehicleMap);
        }
    }
}
