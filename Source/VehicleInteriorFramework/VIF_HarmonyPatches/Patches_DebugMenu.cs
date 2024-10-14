using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_DebugTools
    {
        static Patches_DebugTools()
        {
            var transpiler = AccessTools.Method(typeof(Patches_DebugTools), nameof(Patches_DebugTools.Transpiler));
            foreach (var method in typeof(DebugToolsSpawning).GetDeclaredMethods())
            {
                if (method.IsGenericMethod || method.ContainsGenericParameters) continue;
                VIF_Harmony.Instance.Patch(method, null, null, transpiler);
            }
            foreach (var type in AccessTools.InnerTypes(typeof(DebugToolsSpawning)))
            {
                foreach(var method in type.GetDeclaredMethods())
                {
                    if (method.IsGenericMethod || method.ContainsGenericParameters) continue;
                    VIF_Harmony.Instance.Patch(method, null, null, transpiler);
                }
            }
            foreach (var method in typeof(DebugToolsGeneral).GetDeclaredMethods())
            {
                if (method.IsGenericMethod || method.ContainsGenericParameters) continue;
                VIF_Harmony.Instance.Patch(method, null, null, transpiler);
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Find_CurrentMap, MethodInfoCache.g_VehicleMapUtility_CurrentMap);
        }
    }
}
