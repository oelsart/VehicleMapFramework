using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch]
public static class Patches_Designator_ZoneAdd_MakeNewZone
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in typeof(Designator_ZoneAdd).AllSubclasses())
        {
            var method = AccessTools.DeclaredMethod(type, "MakeNewZone");
            if (method != null && PatchProcessor.ReadMethodBody(method).Any(i => CachedMethodInfo.g_Find_CurrentMap.Equals(i.Value)))
            {
                yield return method;
            }
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatch]
public static class Patches_Designator_DesignateThing
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in typeof(Designator).AllSubclasses())
        {
            var method = AccessTools.DeclaredMethod(type, "DesignateThing");
            if (method != null && PatchProcessor.ReadMethodBody(method).Any(i => CachedMethodInfo.g_Designator_Map.Equals(i.Value)))
            {
                yield return method;
            }
            var method2 = AccessTools.DeclaredMethod(type, "CanDesignateThing");
            if (method2 != null && PatchProcessor.ReadMethodBody(method2).Any(i => CachedMethodInfo.g_Designator_Map.Equals(i.Value)))
            {
                yield return method2;
            }
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(CachedMethodInfo.g_Designator_Map))
            {
                var label = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Pop);
                yield return CodeInstruction.LoadArgument(1);
                yield return new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_MapHeld);
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Brtrue_S, label);
                yield return new CodeInstruction(OpCodes.Pop);
                yield return CodeInstruction.LoadArgument(0);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Designator_Map);
                yield return new CodeInstruction(OpCodes.Nop).WithLabels(label);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.DesignatorManagerUpdate))]
public static class Patch_Designator_SelectedUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_SelectedUpdate = AccessTools.Method(typeof(Designator), nameof(Designator.SelectedUpdate));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(m_SelectedUpdate))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.LoadField(typeof(DesignatorManager), "selectedDesignator");
                yield return CodeInstruction.Call(typeof(Patch_Designator_SelectedUpdate), nameof(SelectedUpdatePostfix));
            }
        }
    }

    public static void SelectedUpdatePostfix(Designator ___selectedDesignator)
    {
        if (Command_FocusVehicleMap.FocuseLockedVehicle != null || ___selectedDesignator is Designator_AreaAllowed) return;

        Command_FocusVehicleMap.FocusedVehicle = null;
        var mousePos = UI.MouseMapPosition();
        if (mousePos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
        {
            Command_FocusVehicleMap.FocusedVehicle = vehicle;
        }
    }
}

[HarmonyPatch(typeof(Designator), nameof(Designator.Map), MethodType.Getter)]
public static class Patch_Designator_Map
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

[HarmonyPatch(typeof(Designator), nameof(Designator.Deselected))]
public static class Patch_Designator_Deselected
{
    public static void Postfix()
    {
        if (Command_FocusVehicleMap.FocuseLockedVehicle == null)
        {
            Command_FocusVehicleMap.FocusedVehicle = null;
        }
    }
}

[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.Deselected))]
public static class Patch_Designator_Build_Deselected
{
    public static void Postfix()
    {
        Patch_Designator_Deselected.Postfix();
    }
}

[HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.DrawDesignations))]
public static class Patch_DesignationManager_DrawDesignations
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(GenGrid), nameof(GenGrid.InNoZoneEdgeArea))]
public static class Patch_GenGrid_InNoZoneEdgeArea
{
    public static void Postfix(ref bool __result, Map map)
    {
        __result &= !map.IsVehicleMapOf(out _);
    }
}

[HarmonyPatch(typeof(Designator_Zone), nameof(Designator_Zone.SelectedUpdate))]
public static class Patch_Designator_Zone_SelectedUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenDraw_DrawFieldEdges));
        codes[pos].operand = CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Designator_Map),
        ]);
        return codes;
    }
}

//利用可能なthingに車上マップ上のthingを含める
[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.ProcessInput))]
public static class Patch_Designator_Build_ProcessInput
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var g_Count = AccessTools.PropertyGetter(typeof(List<Thing>), nameof(List<>.Count));
        var pos = code.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(g_Count));
        code.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Designator_Map),
            CodeInstruction.LoadLocal(4),
            CodeInstruction.Call(typeof(Patch_ItemAvailability_ThingsAvailableAnywhere), nameof(Patch_ItemAvailability_ThingsAvailableAnywhere.AddThingList))
        ]);
        return code;
    }
}

[HarmonyPatch(typeof(DesignationDragger), nameof(DesignationDragger.DraggerUpdate))]
public static class Patch_DesignationDragger_DraggerUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap, CachedMethodInfo.m_ClipInsideVehicleMap);
    }
}

[HarmonyPatch(typeof(Area), nameof(Area.MarkForDraw))]
public static class Patch_Area_MarkForDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
    }
}

//CurrentMapがVehicleMapだったらマップエッジを描くことなんてないよ
[HarmonyPatch(typeof(GenDraw), "DrawMapEdgeLines")]
public static class Patch_GenDraw_DrawMapEdgeLines
{
    public static bool Prefix()
    {
        return !Find.CurrentMap.IsVehicleMapOf(out _);
    }
}