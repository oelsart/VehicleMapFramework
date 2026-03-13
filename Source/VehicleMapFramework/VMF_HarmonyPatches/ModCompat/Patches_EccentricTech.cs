using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat.DefenseGrid;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_EccentricTech
{
    static Patches_EccentricTech()
    {
        if (Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.EccentricTech_DefenseGrid);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricDefenseGrid.PlaceWorker_DefenseProjector", "DrawGhost")]
[PatchLevel(Level.Safe)]
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

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricDefenseGrid.PlaceWorker_ArtillerySensor", "DrawGhost")]
[PatchLevel(Level.Safe)]
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

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricDefenseGrid.Graphic_DefenseConduit", "ShouldLinkWith")]
[PatchLevel(Level.Safe)]
public static class Patch_Graphic_DefenseConduit_ShouldLinkWith
{
    public static void Prefix(ref IntVec3 cell, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref cell, parent);
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricDefenseGrid.CompProjectorOverlay", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompProjectorOverlay_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var f_Vector3_y = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));
        return new CodeMatcher(instructions, generator)
            .MatchStartForward(CodeMatch.StoresField(f_Vector3_y))
            .CreateLabel(out var label)
            .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
            .InsertAndAdvance(
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull),
                new CodeInstruction(OpCodes.Ldc_R4, Altitudes.AltInc * 3f),
                new CodeInstruction(OpCodes.Add)).Advance()
            .MatchStartForward(CodeMatch.StoresField(f_Vector3_y))
            .Repeat(matcher => matcher
                .CreateLabel(out var label2)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                    new CodeInstruction(OpCodes.Brfalse_S, label2),
                    new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                    new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull),
                    new CodeInstruction(OpCodes.Ldc_R4, Altitudes.AltInc * 3f),
                    new CodeInstruction(OpCodes.Add)).Advance())
            .Reset()
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity)).Advance()
            .CreateLabel(out var label3)
            .Insert(
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label3),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ExtraAngle),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.up))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Quaternion), nameof(Quaternion.AngleAxis))),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply))
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricDefenseGrid.CompGeneratorOverlay", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompGeneratorOverlay_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_CompProjectorOverlay_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "MapComponentUpdate")]
[PatchLevel(Level.Sensitive)]
public static class Patch_InterceptorMapComponent_MapComponentUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(MapComponent), nameof(MapComponent.map))))
            .InsertAfter(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Map))
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "Draw")]
[PatchLevel(Level.Cautious)]
public static class Patch_InterceptorMapComponent_Draw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap, CachedMethodInfo.m_ClipInsideVehicleMap);
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricProjectiles.CompProjectileInterceptor", "GetSourceCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_InterceptorMapComponent_GetSourceCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "PaintGrid")]
[PatchLevel(Level.Safe)]
public static class Patch_InterceptorMapComponent_PaintGrid
{
    public static void Prefix(ref MapComponent __instance, object grid, Map ___map)
    {
        if (___map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            var component = vehicle.Map.GetComponent(InterceptorMapComponent);
            if (component is null) return;
            __instance = component;
            mapComponent(grid) = component;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.EccentricTech_DefenseGrid)]
[HarmonyPatch("EccentricProjectiles.InterceptorMapComponent", "UnpaintGrid")]
[PatchLevel(Level.Safe)]
public static class Patch_InterceptorMapComponent_UnpaintGrid
{
    public static void Prefix(ref MapComponent __instance, object grid, Map ___map)
    {
        if (___map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
            var component = vehicle.Map.GetComponent(InterceptorMapComponent);
            if (component is null) return;
            __instance = component;
            mapComponent(grid) = __instance;
        }
    }
}