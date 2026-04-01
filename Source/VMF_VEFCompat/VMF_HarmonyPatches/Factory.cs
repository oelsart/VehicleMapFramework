using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using SmashTools.Rendering;
using Vehicles;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VFEFactory)]
[HarmonyPatch("VanillaFurnitureExpandedFactory.Building_Autofarmer", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_Autofarmer_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Label label = default;
        LocalBuilder vehicle = null;
        return new CodeMatcher(instructions, generator)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_FacingCell))
            .MatchStartBackwards(CodeMatch.Calls(CachedMethodInfo.g_Thing_Rotation))
            .SetOperandAndAdvance(CachedMethodInfo.m_BaseFullRotation_Thing)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_FacingCell))
            .SetOperandAndAdvance(CachedMethodInfo.g_Rot8_FacingCell)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3))
            .SetOperandAndAdvance(CachedMethodInfo.m_Rot8Utility_ToFundVector3)
            .Do(matcher =>
            {
                matcher.CreateLabel(out label);
                matcher.DeclareLocal(typeof(VehiclePawnWithMap), out vehicle);
            })
            .Insert(
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Transform))),
                CodeInstruction.LoadField(typeof(Transform), nameof(Transform.rotation)),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotatedBy))
            .InstructionEnumeration();
    }
}