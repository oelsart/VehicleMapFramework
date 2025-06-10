using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_AbilityComp
    {
        static Patches_AbilityComp()
        {
            var transpiler = AccessTools.Method(typeof(Patches_AbilityComp), nameof(Transpiler));
            foreach (var type in typeof(AbilityComp).AllSubclasses().Concat(typeof(GenClamor)))
            {
                foreach (var method in type.GetDeclaredMethods())
                {
                    if (PatchProcessor.ReadMethodBody(method).Any(i =>
                    {
                        return MethodInfoCache.g_Thing_Position.Equals(i.Value) ||
                        MethodInfoCache.g_LocalTargetInfo_Cell.Equals(i.Value) ||
                        MethodInfoCache.g_Thing_Map.Equals(i.Value) ||
                        MethodInfoCache.g_Thing_MapHeld.Equals(i.Value) ||
                        MethodInfoCache.m_OccupiedRect.Equals(i.Value) ||
                        MethodInfoCache.m_BreadthFirstTraverse.Equals(i.Value);
                    }))
                    {
                        VMF_Harmony.Instance.Patch(method, null, null, transpiler);
                    }
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_OccupiedRect, MethodInfoCache.m_MovedOccupiedRect)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap)
                .MethodReplacer(MethodInfoCache.m_BreadthFirstTraverse, MethodInfoCache.m_BreadthFirstTraverseAcrossMaps);
        }
    }
}
