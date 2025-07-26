using DubsBadHygiene;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_DBH
{
    public const string Category = "VMF_Patches_DBH";

    static Patches_DBH()
    {
        VMF_Harmony.PatchCategory(Category);
        if (DubsBadHygiene.Settings.LiteMode)
        {
            DefDatabase<ThingDef>.GetNamed("VMF_PipeConnector").comps.RemoveAll(c => c is CompProperties_PipeConnectorDBH);
        }
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch(typeof(CompPipe), nameof(CompPipe.Props), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_CompResource_Props
{
    public static void Postfix(CompPipe __instance, ref CompProperties_Pipe __result)
    {
        if (__instance is CompPipeConnectorDBH connector)
        {
            dummy.mode = connector.mode;
            dummy.stuffed = connector.Props.stuffed;
            dummy.vertPipe = connector.Props.vertPipe;
            __result = dummy;
        }
    }

    private static readonly CompProperties_Pipe dummy = new();
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch(typeof(PlaceWorker_SewageArea), nameof(PlaceWorker_SewageArea.DrawGhost))]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_SewageArea_DrawGhost
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld);
        var label = generator.DefineLabel();
        var label2 = generator.DefineLabel();
        var f_visibleMap = codes[pos].operand;

        codes[pos].labels.Add(label2);
        codes.InsertRange(pos - 1,
        [
            CodeInstruction.LoadArgument(5),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Br_S, label2),
            new CodeInstruction(OpCodes.Pop).WithLabels(label)
        ]);
        pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenDraw_DrawFieldEdges));
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadLocal(0),
            new CodeInstruction(OpCodes.Ldfld, f_visibleMap)
        ]);
        return codes.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap)
            .MethodReplacer(CachedMethodInfo.m_GenDraw_DrawFieldEdges, CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges);
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_SewageArea_DrawGhost_Predicate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(PlaceWorker_SewageArea),
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawGhost>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var m_GetFirstBuilding = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetFirstBuilding));
        var pos = codes.FindIndex(c => c.Calls(m_GetFirstBuilding));
        var label = generator.DefineLabel();
        var map = generator.DeclareLocal(typeof(Map));

        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Stloc_S, map),
            new CodeInstruction(OpCodes.Ldloc_S, map),
            CodeInstruction.Call(typeof(GenGrid), nameof(GenGrid.InBounds), [typeof(IntVec3), typeof(Map)]),
            new CodeInstruction(OpCodes.Brtrue_S, label),
            new CodeInstruction(OpCodes.Ldc_I4_0),
            new CodeInstruction(OpCodes.Ret),
            CodeInstruction.LoadArgument(1).WithLabels(label),
            new CodeInstruction(OpCodes.Ldloc_S, map)
        ]);
        return codes;
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch(typeof(MapComponent_Hygiene), nameof(MapComponent_Hygiene.CanHaveSewage))]
[PatchLevel(Level.Safe)]
public static class Patch_MapComponent_Hygiene_CanHaveSewage
{
    public static bool Prefix(IntVec3 c, Map ___map, ref bool __result)
    {
        if (!c.InBounds(___map))
        {
            __result = false;
            return false;
        }
        return true;
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch(typeof(MapComponent_Hygiene), nameof(MapComponent_Hygiene.MapComponentUpdate))]
[PatchLevel(Level.Cautious)]
public static class Patch_MapComponent_Hygiene_MapComponentUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap, CachedMethodInfo.m_ClipInsideVehicleMap);
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch("DubsBadHygiene.SectionLayer_PipeOverlay", "DrawAllTileOverlays")]
[PatchLevel(Level.Sensitive)]
public static class Patch_SectionLayer_PipeOverlay_DrawAllTileOverlays
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Beq_S);
        var label = codes[pos].operand;
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        codes.InsertRange(pos + 1,
        [
            CodeInstruction.LoadLocal(2),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brtrue_S, label)
        ]);
        return codes;
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch(typeof(Graphic_LinkedPipe), nameof(Graphic_LinkedPipe.ShouldLinkWith))]
[PatchLevel(Level.Safe)]
public static class Patch_Graphic_LinkedPipeDBH_ShouldLinkWith
{
    public static void Prefix(IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch(typeof(Building_AssignableFixture), nameof(Building_AssignableFixture.Print))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_AssignableFixture_Print
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R4 && (float)c.operand == 0f);

        codes.Replace(codes[pos], new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
        codes.Insert(pos, CodeInstruction.LoadArgument(0));
        return codes;
    }
}

[HarmonyPatchCategory(Patches_DBH.Category)]
[HarmonyPatch("DubsBadHygiene.Building_StallDoor", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_StallDoor_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = Patch_Building_Door_DrawMovers.Transpiler(instructions, generator).ToList();
        var f_Vector3_y = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));
        var label = generator.DefineLabel();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld && c.OperandIs(f_Vector3_y));

        codes[pos].labels.Add(label);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull2)
        ]);
        return codes;
    }
}
