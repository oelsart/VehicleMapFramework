using HarmonyLib;
using PipeSystem;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VEF.Hediffs;
using VEF.Weapons;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_VEF
{
    public const string CategoryCore = "VMF_Patches_VEF";

    public const string CategoryArchitect = "VMF_Patches_VFE_Architect";

    public const string CategorySecurity = "VMF_Patches_VFE_Security";

    public const string CategoryVVE = "VMF_Patches_VVE";

    public const string CategoryPirates = "VMF_Patches_VFE_Pirates";

    public const string CategoryMechanoid = "VMF_Patches_VFE_Mechanoid";

    static Patches_VEF()
    {
        VMF_Harmony.PatchCategory(CategoryCore);
        if (ModCompat.VFEArchitect)
        {
            VMF_Harmony.PatchCategory(CategoryArchitect);
        }
        if (ModCompat.VFESecurity.Active)
        {
            VMF_Harmony.PatchCategory(CategorySecurity);
        }
        if (ModCompat.VVE.Active)
        {
            VMF_Harmony.PatchCategory(CategoryVVE);
        }
        if (ModCompat.VFEPirates)
        {
            VMF_Harmony.PatchCategory(CategoryPirates);
        }
        if (ModCompat.VFEMechanoid.Active)
        {
            VMF_Harmony.PatchCategory(CategoryMechanoid);
        }
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(CompResource), nameof(CompResource.Props), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_CompResource_Props
{
    public static void Postfix(CompResource __instance, ref CompProperties_Resource __result)
    {
        if (__instance is CompPipeConnectorVEF connector)
        {
            dummy.pipeNet = connector.pipeNet;
            dummy.soundAmbient = __result.soundAmbient;
            __result = dummy;
        }
    }

    private static readonly CompProperties_Resource dummy = new CompProperties_Resource();
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(PipeNetManager), nameof(PipeNetManager.UnregisterConnector))]
[PatchLevel(Level.Safe)]
public static class Patch_PipeNetManager_UnregisterConnector
{
    public static void Prefix(PipeNetManager __instance, CompResource comp)
    {
        var pipeNetMap = comp.PipeNet.map;
        if (__instance.map != pipeNetMap)
        {
            var component = MapComponentCache<PipeNetManager>.GetComponent(pipeNetMap);
            var connectors = comp.PipeNet.connectors.Where(c => c.parent.Map == pipeNetMap);
            var newNet = PipeNetMaker.MakePipeNet(connectors, pipeNetMap, comp.PipeNet.def);
            component.pipeNets.Add(newNet);
            CompPipeConnectorVEF.pipeNetCount(MapComponentCache<PipeNetManager>.GetComponent(__instance.map))++;
        }
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(PipeNet), nameof(PipeNet.Merge))]
[PatchLevel(Level.Safe)]
public static class Patch_PipeNet_Merge
{
    public static bool Prefix(ref PipeNet __instance, ref PipeNet otherNet)
    {
        if (__instance.map.IsVehicleMapOf(out _) && otherNet.map != __instance.map)
        {
            otherNet.Merge(__instance);
            return false;
        }
        return true;
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(Graphic_LinkedPipe), nameof(Graphic_LinkedPipe.ShouldLinkWith))]
[PatchLevel(Level.Safe)]
public static class Patch_Graphic_LinkedPipeVEF_ShouldLinkWith
{
    public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, AccessTools.Method(typeof(Patch_Graphic_LinkedPipeVEF_ShouldLinkWith), nameof(MapModified)));
    }

    private static Map MapModified(Thing thing)
    {
        if (thing.TryGetComp<CompResource>(out var comp) && thing.Map != comp.PipeNet.map)
        {
            return comp.PipeNet.map;
        }
        return thing.Map;
    }
}


[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(CompResourceStorage), nameof(CompResourceStorage.PostDraw))]
[PatchLevel(Level.Safe)]
public static class Patch_CompResourceStorage_PostDraw
{
    public static void Prefix(CompResourceStorage __instance, ref GenDraw.FillableBarRequest ___request)
    {
        var fullRot = __instance.parent.BaseFullRotationAsRot4();
        var offset = (__instance.Props.centerOffset + (Vector3.up * 0.1f)).RotatedBy(new Rot8(fullRot.AsInt).AsAngle);
        if (__instance.parent.Graphic.WestFlipped && __instance.parent.BaseRotationVehicleDraw() == Rot4.West)
        {
            offset = offset.RotatedBy(180f);
        }
        ___request.center = __instance.parent.DrawPos + offset;
        Rot8Utility.Rotate(ref fullRot, RotationDirection.Clockwise);
        Rot8Utility.rot4Int(ref ___request.rotation) = fullRot.AsByte;
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(ExpandableProjectile), nameof(ExpandableProjectile.StartingPosition), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_ExpandableProjectile_StartingPosition
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch(typeof(ShieldsSystem), nameof(ShieldsSystem.OnPawnSpawn))]
[PatchLevel(Level.Safe)]
public static class Patch_ShieldsSystem_OnPawnSpawn
{
    private static Dictionary<string, HashSet<int>> trackedPawns = [];

    public static bool Prefix(Pawn __instance)
    {
        if (__instance == null) return false;

        try
        {
            // Simple tracking to prevent duplicate key errors
            string pawnKey = __instance.ThingID;
            int pawnID = __instance.thingIDNumber;

            // Use our own tracking system to prevent duplicates
            if (!trackedPawns.ContainsKey(pawnKey))
            {
                trackedPawns[pawnKey] = [];
            }

            if (trackedPawns[pawnKey].Contains(pawnID))
            {
                // We've seen this pawn before, skip the original method
                return false;
            }

            // First time seeing this pawn, add it to our tracker
            trackedPawns[pawnKey].Add(pawnID);

            // Clean up if we're tracking too many versions of this pawn
            if (trackedPawns[pawnKey].Count > 10)
            {
                trackedPawns[pawnKey].Clear();
                trackedPawns[pawnKey].Add(pawnID);
            }

            return true; // Let the original method run for new pawns
        }
        catch (Exception ex)
        {
            Log.Error($"Exception in shield system: {ex.Message}");
            return true; // If anything goes wrong, let the original method run
        }
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch("VEF.Weapons.Verb_ShootCone", "DrawLines")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCone_DrawLines
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotationAsRot4)
            .MethodReplacer(CachedMethodInfo.g_Rot4_AsQuat, CachedMethodInfo.m_Rot8_AsQuatRef);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch("VEF.Weapons.Verb_ShootCone", "DrawConeRounded")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCone_DrawConeRounded
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotationAsRot4);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch("VEF.Weapons.Verb_ShootCone", "CanHitTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCone_CanHitTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryCore)]
[HarmonyPatch("VEF.Weapons.Verb_ShootCone", "InCone")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootCone_InCone
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Rot4_AsAngle, CachedMethodInfo.g_Rot8_AsAngle);
    }
}