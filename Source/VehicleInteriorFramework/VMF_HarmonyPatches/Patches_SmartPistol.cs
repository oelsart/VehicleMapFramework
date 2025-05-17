using HarmonyLib;
using System.Collections.Generic;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_SmartPistol
    {
        static Patches_SmartPistol()
        {
            if (ModCompat.SmartPistol)
            {
                VMF_Harmony.Instance.Patch(AccessTools.Method("RB_SmartPistol.TargetCandidateUtil:GetSubTargets"),
                    transpiler: AccessTools.Method(typeof(Patch_TargetCandidateUtil_GetSubTargets), nameof(Patch_TargetCandidateUtil_GetSubTargets.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("RB_SmartPistol.Mote_LockedCurve:Tick"),
                    transpiler: AccessTools.Method(typeof(Patch_Mote_LockedCurve_Tick), nameof(Patch_Mote_LockedCurve_Tick.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("RB_SmartPistol.Mote_LockedCurve:DrawAt"),
                    transpiler: AccessTools.Method(typeof(Patch_Mote_LockedCurve_Tick), nameof(Patch_Mote_LockedCurve_Tick.Transpiler)));
            }
        }
    }

    public static class Patch_TargetCandidateUtil_GetSubTargets
    {
        public static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle);
        }
    }

    public static class Patch_Mote_LockedCurve_Tick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    public static class Patch_Mote_LockedCurve_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }
}