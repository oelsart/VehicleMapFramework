using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_EccentricTech
{
    public const string Category = "VMF_Patches_EccentricTech_DefenseGrid";

    static Patches_EccentricTech()
    {
        if (ModCompat.DefenseGrid.Active)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricDefenseGrid.PlaceWorker_DefenseProjector", "DrawGhost")]
public static class Patch_PlaceWorker_DefenseProjector_DrawGhost
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(ref IntVec3 center, Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) || (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null)
        {
            center = center.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricDefenseGrid.PlaceWorker_ArtillerySensor", "DrawGhost")]
public static class Patch_PlaceWorker_ArtillerySensor_DrawGhost
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(ref IntVec3 center, Thing thing)
    {
        if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) || (vehicle = Command_FocusVehicleMap.FocusedVehicle) != null)
        {
            center = center.ToBaseMapCoord(vehicle);
        }
    }
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricDefenseGrid.Graphic_DefenseConduit", "ShouldLinkWith")]
public static class Patch_Graphic_DefenseConduit_ShouldLinkWith
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(ref IntVec3 cell, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref cell, parent);
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricDefenseGrid.CompProjectorOverlay", "PostDraw")]
public static class Patch_CompProjectorOverlay_PostDraw
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var f_Vector3_y = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));
        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(f_Vector3_y))
            {
                var label = generator.DefineLabel();
                var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent));
                yield return new CodeInstruction(OpCodes.Ldloca_S, vehicle);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf);
                yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                yield return new CodeInstruction(OpCodes.Ldloc_S, vehicle);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull2);
                yield return instruction.WithLabels(label);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "MapComponentUpdate")]
public static class Patch_InterceptorMapComponent_MapComponentUpdate
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var f_map = AccessTools.Field(typeof(MapComponent), nameof(MapComponent.map));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_map)) + 1;
        codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Map));
        return codes;
    }
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "Draw")]
public static class Patch_InterceptorMapComponent_Draw
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_InterceptorMapComponent_MapComponentUpdate.Transpiler(instructions);
}

[HarmonyPatchCategory(Patches_EccentricTech.Category)]
[HarmonyPatch("EccentricProjectiles.CompProjectileInterceptor", "ShouldDrawField")]
public static class Patch_CompProjectileInterceptor_ShouldDrawField
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}
