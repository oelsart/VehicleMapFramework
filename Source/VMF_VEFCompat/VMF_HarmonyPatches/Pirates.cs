using HarmonyLib;
using System.Collections.Generic;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "DrawLines")]
    public static class Patch_Verb_ShootCone_DrawLines
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
                .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotationAsRot4)
                .MethodReplacer(CachedMethodInfo.g_Rot4_AsQuat, CachedMethodInfo.m_Rot8_AsQuatRef);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "DrawConeRounded")]
    public static class Patch_Verb_ShootCone_DrawConeRounded
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
                .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotationAsRot4);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "CanHitTarget")]
    public static class Patch_Verb_ShootCone_CanHitTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
                .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
                .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VFE_Pirates")]
    [HarmonyPatch("VFEPirates.Verb_ShootCone", "InCone")]
    public static class Patch_Verb_ShootCone_InCone
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Rot4_AsAngle, CachedMethodInfo.g_Rot8_AsAngle);
        }
    }
}
