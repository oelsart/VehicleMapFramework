using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_ExosuitFramework
{
    public const string Category = "VMF_Patches_ExosuitFramework";

    static Patches_ExosuitFramework()
    {
        if (ModCompat.ExosuitFramework)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_ExosuitFramework.Category)]
[HarmonyPatch("WalkerGear.CompBuildingExtraRenderer", "PostPrintOnto")]
public static class Patch_CompBuildingExtraRenderer_PostPrintOnto
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

        codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
        codes.Insert(pos, new CodeInstruction(OpCodes.Dup));
        return codes;
    }
}

[HarmonyPatchCategory(Patches_ExosuitFramework.Category)]
[HarmonyPatch("WalkerGear.WG_AbilityVerb_QuickJump", "DoJump")]
[HarmonyPatch([typeof(Pawn), typeof(Map), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool)])]
public static class Patch_WG_AbilityVerb_QuickJump_DoJump
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(Pawn pawn, Map targetMap, ref LocalTargetInfo currentTarget)
    {
        if (pawn.IsOnNonFocusedVehicleMapOf(out _))
        {
            var positionOnBaseMap = pawn.PositionOnBaseMap();
            currentTarget = new IntVec3(positionOnBaseMap.x, positionOnBaseMap.y, Math.Min(positionOnBaseMap.z + 25, CellRect.WholeMap(targetMap).maxZ));
        }
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_ExosuitFramework.Category)]
[HarmonyPatch("WalkerGear.WG_PawnFlyer", "RespawnPawn")]
public static class Patch_WG_PawnFlyer_RespawnPawn
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_ExosuitFramework.Category)]
[HarmonyPatch("WalkerGear.WG_PawnFlyer", "SpawnSetup")]
public static class Patch_WG_PawnFlyer_SpawnSetup
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(ref Thing ___eBay, Map map)
    {
        if (___eBay == null)
        {
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            ___eBay = maps.SelectMany(m => m.listerBuildings.allBuildingsColonist).FirstOrDefault(b => b.GetType() == t_Building_EjectorBay);
        }
    }

    private static Type t_Building_EjectorBay = AccessTools.TypeByName("WalkerGear.Building_EjectorBay");
}

[HarmonyPatchCategory(Patches_ExosuitFramework.Category)]
[HarmonyPatch("WalkerGear.Building_EjectorBay", "DynamicDrawPhaseAt")]
public static class Patch_Building_EjectorBay_DynamicDrawPhaseAt
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotation);
    }
}

[HarmonyPatchCategory(Patches_ExosuitFramework.Category)]
[HarmonyPatch("WalkerGear.Building_MaintenanceBay", "DynamicDrawPhaseAt")]
public static class Patch_Building_MaintenanceBay_DynamicDrawPhaseAt
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(Building __instance, Pawn ___cachePawn)
    {
        ___cachePawn?.Rotation = __instance.BaseRotation().Opposite;
    }
}
