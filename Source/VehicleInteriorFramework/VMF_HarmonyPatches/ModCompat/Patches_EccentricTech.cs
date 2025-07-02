using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_EccentricTech
{
    static Patches_EccentricTech()
    {
        if (ModCompat.DefenseGrid.Active)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_EccentricTech_DefenseGrid");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricDefenseGrid.PlaceWorker_DefenseProjector", "DrawGhost")]
public static class Patch_PlaceWorker_DefenseProjector_DrawGhost
{
    public static void Prefix(ref IntVec3 center, Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) || (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null)
        {
            center = center.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricDefenseGrid.PlaceWorker_ArtillerySensor", "DrawGhost")]
public static class Patch_PlaceWorker_ArtillerySensor_DrawGhost
{
    public static void Prefix(ref IntVec3 center, Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) || (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null)
        {
            center = center.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricDefenseGrid.Graphic_DefenseConduit", "ShouldLinkWith")]
public static class Patch_Graphic_DefenseConduit_ShouldLinkWith
{
    public static void Prefix(ref IntVec3 cell, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref cell, parent);
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricDefenseGrid.CompProjectorOverlay", "PostDraw")]
public static class Patch_CompProjectorOverlay_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var YOffset = generator.DeclareLocal(typeof(float));
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var label = generator.DefineLabel();
        instructions.ElementAt(0).labels.Add(label);

        yield return CodeInstruction.LoadArgument(0);
        yield return CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent));
        yield return new CodeInstruction(OpCodes.Ldloca_S, vehicle);
        yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf);
        yield return new CodeInstruction(OpCodes.Brfalse_S, label);
        yield return new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.altitudeOffsetFull);
        yield return new CodeInstruction(OpCodes.Stloc_S, YOffset);

        var f_Vector3_y = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Stfld && instruction.OperandIs(f_Vector3_y))
            {
                yield return new CodeInstruction(OpCodes.Ldloc_S, YOffset);
                yield return new CodeInstruction(OpCodes.Add);
            }
            yield return instruction;
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "MapComponentUpdate")]
public static class Patch_InterceptorMapComponent_MapComponentUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var f_map = AccessTools.Field(typeof(MapComponent), nameof(MapComponent.map));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_map)) + 1;
        codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Map));
        return codes;
    }
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "Draw")]
public static class Patch_InterceptorMapComponent_Draw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_InterceptorMapComponent_MapComponentUpdate.Transpiler(instructions);
}

[HarmonyPatchCategory("VMF_Patches_EccentricTech_DefenseGrid")]
[HarmonyPatch("EccentricProjectiles.CompProjectileInterceptor", "ShouldDrawField")]
public static class Patch_CompProjectileInterceptor_ShouldDrawField
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}
