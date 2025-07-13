using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Odyssey
{
    static Patches_Odyssey()
    {
        if (ModsConfig.OdysseyActive)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_Odyssey");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_Odyssey")]
[HarmonyPatch(typeof(Building_GravEngine), "UpdateSubstructureIfNeeded")]
public static class Patch_Building_GravEngine_UpdateSubstructureIfNeeded
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        void ReplaceType(Type type, Type type2)
        {
            codes.MatchStartForward(new CodeMatch(OpCodes.Ldtoken, type));
            codes.CreateLabelWithOffsets(1, out var label);
            codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
            codes.InsertAfterAndAdvance(
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Thing_Map),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldtoken, type2));
        }
        ReplaceType(typeof(SectionLayer_GravshipHull), typeof(SectionLayer_GravshipHullOnVehicle));
        ReplaceType(typeof(SectionLayer_SubstructureProps), typeof(SectionLayer_SubstructurePropsOnVehicle));
        return codes.Instructions();
    }
}

[HarmonyPatchCategory("VMF_Patches_Odyssey")]
[HarmonyPatch(typeof(Building_VacBarrier), "DrawAt")]
public static class Patch_Building_VacBarrier_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
        codes.MatchStartForward(CodeMatch.LoadsConstant(0f));
        codes.CreateLabelWithOffsets(1, out var label);
        codes.InsertAfter(
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Angle),
            new CodeInstruction(OpCodes.Neg),
            new CodeInstruction(OpCodes.Add));
        return codes.Instructions().MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
    }
}