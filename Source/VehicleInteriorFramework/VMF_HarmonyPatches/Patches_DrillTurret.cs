using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_DrillTurret
    {
        static Patches_DrillTurret()
        {
            if (ModCompat.DrillTurret)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_DrillTurret");

                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:LookForNewTarget"), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_LookForNewTarget), nameof(Patch_Building_DrillTurret_LookForNewTarget.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:IsValidTargetAt"), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_IsValidTargetAt), nameof(Patch_Building_DrillTurret_IsValidTargetAt.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:IsValidTargetAtForGizmo"), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_IsValidTargetAtForGizmo), nameof(Patch_Building_DrillTurret_IsValidTargetAtForGizmo.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:DrillRock"), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_DrillRock), nameof(Patch_Building_DrillTurret_DrillRock.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("DrillTurret.Building_DrillTurret"), t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<SelectTarget>"))), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_SelectTarget), nameof(Patch_Building_DrillTurret_SelectTarget.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:SetForcedTarget"), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_SetForcedTarget), nameof(Patch_Building_DrillTurret_SetForcedTarget.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:ComputeDrawingParameters"), prefix: AccessTools.Method(typeof(Patch_Building_DrillTurret_ComputeDrawingParameters), nameof(Patch_Building_DrillTurret_ComputeDrawingParameters.Prefix)), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_ComputeDrawingParameters), nameof(Patch_Building_DrillTurret_ComputeDrawingParameters.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("DrillTurret.Building_DrillTurret:DrawAt"), prefix: AccessTools.Method(typeof(Patch_Building_DrillTurret_DrawAt), nameof(Patch_Building_DrillTurret_DrawAt.Prefix)), transpiler: AccessTools.Method(typeof(Patch_Building_DrillTurret_DrawAt), nameof(Patch_Building_DrillTurret_DrawAt.Transpiler)));
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "LookForNewTarget")]
    public static class Patch_Building_DrillTurret_LookForNewTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "IsValidTargetAt")]
    public static class Patch_Building_DrillTurret_IsValidTargetAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight2, MethodInfoCache.m_GenSightOnVehicle_LineOfSight2);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "IsValidTargetAtForGizmo")]
    public static class Patch_Building_DrillTurret_IsValidTargetAtForGizmo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight2, MethodInfoCache.m_GenSightOnVehicle_LineOfSight2);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "DrillRock")]
    public static class Patch_Building_DrillTurret_DrillRock
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch]
    public static class Patch_Building_DrillTurret_SelectTarget
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("DrillTurret.Building_DrillTurret"), t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<SelectTarget>")));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "SetForcedTarget")]
    public static class Patch_Building_DrillTurret_SetForcedTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "ComputeDrawingParameters")]
    public static class Patch_Building_DrillTurret_ComputeDrawingParameters
    {
        private static Vector3? overridePos;

        public static void Prefix(Thing __instance)
        {
            if (__instance.IsOnVehicleMapOf(out var vehicle))
            {
                overridePos = __instance.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.Projectile).ToBaseMapCoord(vehicle);
                return;
            }
            overridePos = null;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ToVector3Override = AccessTools.Method(typeof(Patch_Building_DrillTurret_ComputeDrawingParameters), nameof(ToVector3Override));
            return instructions.MethodReplacer(MethodInfoCache.m_IntVec3_ToVector3ShiftedWithAltitude2, m_ToVector3Override);
        }

        public static Vector3 ToVector3Override(ref IntVec3 c, float AddedAltitude)
        {
            return overridePos ?? c.ToVector3ShiftedWithAltitude(AddedAltitude);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DrillTurret")]
    [HarmonyPatch("DrillTurret.Building_DrillTurret", "DrawAt")]
    public static class Patch_Building_DrillTurret_DrawAt
    {
        public static void Prefix(Thing __instance)
        {
            Patch_Building_DrillTurret_ComputeDrawingParameters.Prefix(__instance);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Building_DrillTurret_ComputeDrawingParameters.Transpiler(instructions);
        }
    }
}
