using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Odyssey
{
    static Patches_Odyssey()
    {
        if (ModsConfig.OdysseyActive)
        {
            VMF_Harmony.PatchCategory(PatchCategories.Odyssey);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.Odyssey)]
[HarmonyPatch(typeof(Building_GravEngine), "UpdateSubstructureIfNeeded")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_GravEngine_UpdateSubstructureIfNeeded
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        ReplaceType(typeof(SectionLayer_GravshipHull), typeof(SectionLayer_GravshipHullOnVehicle));
        ReplaceType(typeof(SectionLayer_SubstructureProps), typeof(SectionLayer_SubstructurePropsOnVehicle));
        return codes.Instructions();

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
    }
}

[HarmonyPatchCategory(PatchCategories.Odyssey)]
[HarmonyPatch(typeof(Building_GravEngine), nameof(Building_GravEngine.DeSpawn))]
[PatchLevel(Level.Safe)]
public static class Patch_Building_GravEngine_DeSpawn
{
    public static void Prefix(Building_GravEngine __instance)
    {
        if (__instance.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned && !GravshipVehicleUtility.GravshipProcessInProgress)
        {
            var loc = __instance.Position;
            var rot = __instance.Rotation;
            LongEventHandler.QueueLongEvent(() =>
            {
                GravshipVehicleUtility.PlaceGravshipVehicleUnSpawned(__instance, loc, rot, vehicle, true);
            }, "VMF_GravshipVehicleDestroyed".Translate(), false, null, false);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.Odyssey)]
[HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.CanRemoveFoundationAt))]
[PatchLevel(Level.Safe)]
public static class Patch_TerrainGrid_CanRemoveFoundationAt
{
    public static void Postfix(ref bool __result, Map ___map)
    {
        __result &= !___map.IsVehicleMapOf(out var vehicle) || !vehicle.def.HasModExtension<VehicleMapProps_Gravship>();
    }
}

//ThingがあればThing.Map、なければFocusedVehicle.VehicleMap、それもなければFind.CurrentMapを参照するようにする
[HarmonyPatchCategory(PatchCategories.Odyssey)]
[HarmonyPatch(typeof(PlaceWorker_GravshipThruster), nameof(PlaceWorker_GravshipThruster.DrawGhost))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_GravshipThruster_DrawGhost
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenDraw_DrawFieldEdges));
        codes.CreateLabel(out var label);
        codes.DefineLabel(out var label2);
        codes.InsertAndAdvance(
            CodeInstruction.LoadArgument(5),
            new CodeInstruction(OpCodes.Brfalse_S, label2),
            CodeInstruction.LoadArgument(5),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
            new CodeInstruction(OpCodes.Br_S, label),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_VehicleMapUtility_CurrentMap).WithLabels(label2)
            );
        codes.Operand = CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges;
        return codes.Instructions();
    }
}

/// <summary>
/// PlayerHomeが無くてそれ以外のマップが2つ以上ある時キャッシュを更新し続けてしまう問題があるため、ルデオンがこれを修正するまでの一時パッチ
/// </summary>
[HarmonyPatchCategory(PatchCategories.Odyssey)]
[HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.GetPlayerGravEngine_NewTemp))]
[PatchLevel(Level.Sensitive)]
public static class Patch_GravshipUtility_GetPlayerGravEngine_NewTemp
{
    private static readonly ConditionalWeakTable<Map, StrongBox<(int tick, Building_GravEngine engine)>> cacheTable = [];

    private static bool working;
    
    public static void Prefix(Map map, ref int ___lastCachedEngineTick, ref int ___lastCachedEngineMapID,
        ref Building_GravEngine ___cachedGravEngine)
    {
        if (working) return;

        var cache = cacheTable.GetOrCreateValue(map);
        if (cache.Value.tick != Find.TickManager.TicksGame)
        {
            working = true;
            try
            {
                cache.Value.tick = Find.TickManager.TicksGame;
                cache.Value.engine = GravshipUtility.GetPlayerGravEngine_NewTemp(map);
            }
            finally
            {
                working = false;
            }
        }
        ___lastCachedEngineTick = cache.Value.tick;
        ___lastCachedEngineMapID = map.uniqueID;
        ___cachedGravEngine = cache.Value.engine;
    }
}