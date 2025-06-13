using HarmonyLib;
using LudeonTK;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_DebugTools
    {
        static Patches_DebugTools()
        {
            if (VehicleInteriors.settings.debugToolPatches)
            {
                ApplyPatches();
            }
        }

        public static void ApplyPatches(bool unpatch = false)
        {
            var transpiler = AccessTools.Method(typeof(Patches_DebugTools), nameof(Transpiler));
            void Patch(MethodInfo method)
            {
                if (method.IsGenericMethod || method.ContainsGenericParameters) return;
                if (unpatch)
                {
                    VMF_Harmony.Instance.Unpatch(method, transpiler);
                }
                else
                {
                    VMF_Harmony.Instance.Patch(method, transpiler: transpiler);
                }
            }

            Patch(AccessTools.FindIncludingInnerTypes(typeof(DebugActionNode), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<Enter>"))));
            foreach (var method in typeof(DebugToolsSpawning).GetDeclaredMethods())
            {
                Patch(method);
            }
            foreach (var type in AccessTools.InnerTypes(typeof(DebugToolsSpawning)))
            {
                foreach (var method in type.GetDeclaredMethods())
                {
                    Patch(method);
                }
            }
            foreach (var method in typeof(DebugToolsGeneral).GetDeclaredMethods())
            {
                Patch(method);
            }
            foreach (var method in typeof(DebugToolsPawns).GetDeclaredMethods())
            {
                Patch(method);
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
        }
    }
}
