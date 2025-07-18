using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI))]
public static class Patch_ThingOverlays_ThingOverlaysOnGUI
{
    public static void Postfix()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        var vehicles = VehiclePawnWithMapCache.AllVehiclesOn(Find.CurrentMap);
        if (vehicles.Count == 0)
        {
            return;
        }
        CellRect currentViewRect = Find.CameraDriver.CurrentViewRect;
        foreach (var thing in vehicles.SelectMany(v => v.VehicleMap.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay)))
        {
            if (currentViewRect.Contains(thing.PositionOnBaseMap())/* && !Find.CurrentMap.fogGrid.IsFogged(thing.PositionOnBaseMap())*/) //車両マップである時点でFoggedはスキップしていいはず
            {
                try
                {
                    thing.DrawGUIOverlay();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat(
                    [
                        "Exception drawing ThingOverlay for ",
                        thing,
                        ": ",
                        ex
                    ]));
                }
            }
        }
    }
}

//VehicleMapはコロニストバーに表示させない
[HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
public static class Patch_ColonistBar_CheckRecacheEntries
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var g_Find_Maps = AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps));
        var pos = codes.FindIndex(c => c.Calls(g_Find_Maps)) + 1;
        codes.Insert(pos, CodeInstruction.Call(typeof(Patch_ColonistBar_CheckRecacheEntries), nameof(ExcludeVehicleMaps)));
        return codes;
    }

    private static IEnumerable<Map> ExcludeVehicleMaps(this IEnumerable<Map> maps)
    {
        return maps?.Where(m => !m.IsVehicleMapOf(out var vehicle) || vehicle.GetAerialVehicle() != null || vehicle.GetVehicleCaravan() != null || vehicle.GetCaravan() != null);
    }
}

//左下のセル情報の表示。車両マップ上にマウスオーバーされている時はその車両マップの情報を表示する
[HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
public static class Patch_MouseoverReadout_MouseoverReadoutOnGUI
{
    public static void PrefixCommon(ref object[] __state)
    {
        if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
        {
            sbyte index;
            VehiclePawnWithMap vehicle2;
            __state = [index = Current.Game.currentMapIndex, vehicle2 = Command_FocusVehicleMap.FocusedVehicle];
            Current.Game.currentMapIndex = (sbyte)vehicle.VehicleMap.Index;
            Command_FocusVehicleMap.FocusedVehicle = vehicle;
            if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
            {
                Current.Game.currentMapIndex = index;
                Command_FocusVehicleMap.FocusedVehicle = vehicle2;
                __state = null;
            }
        }
    }

    //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
    public static void Prefix(ref object[] __state)
    {
        if (Event.current.type != EventType.Repaint || Find.MainTabsRoot.OpenTab != null)
        {
            return;
        }
        PrefixCommon(ref __state);
    }

    //FocusedVehicleをもとに戻しておく
    public static void Finalizer(object[] __state)
    {
        if (__state is not null)
        {
            Current.Game.currentMapIndex = (sbyte)__state[0];
            Command_FocusVehicleMap.FocusedVehicle = (VehiclePawnWithMap)__state[1];
        }
    }
}

//Alt押した時のセル情報表示。MouseoverReadoutOnGUIと全く同じ
[HarmonyPatch(typeof(CellInspectorDrawer), "DrawMapInspector")]
public static class Patch_CellInspectorDrawer_DrawMapInspector
{
    //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
    public static void Prefix(ref object[] __state)
    {
        Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
    }

    //FocusedVehicleをもとに戻しておく
    public static void Finalizer(object[] __state)
    {
        if (__state is not null)
        {
            Current.Game.currentMapIndex = (sbyte)__state[0];
            Command_FocusVehicleMap.FocusedVehicle = (VehiclePawnWithMap)__state[1];
        }
    }
}

[HarmonyPatch(typeof(CellInspectorDrawer), nameof(CellInspectorDrawer.Update))]
public static class Patch_CellInspectorDrawer_Update
{   
    //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
    public static void Prefix(ref object[] __state)
    {
        if (!KeyBindingDefOf.ShowCellInspector.IsDown) return;
        Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
    }

    //FocusedVehicleをもとに戻しておく
    public static void Finalizer(object[] __state)
    {
        if (__state is not null)
        {
            Current.Game.currentMapIndex = (sbyte)__state[0];
            Command_FocusVehicleMap.FocusedVehicle = (VehiclePawnWithMap)__state[1];
        }
    }
}

//Alt押した時のセルの美しさ
[HarmonyPatch(typeof(BeautyDrawer), "DrawBeautyAroundMouse")]
public static class Patch_BeautyDrawer_DrawBeautyAroundMouse
{
    //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
    public static void Prefix(ref object[] __state)
    {
        Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
    }

    //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_LabelDrawPosFor = AccessTools.Method(typeof(GenMapUI), nameof(GenMapUI.LabelDrawPosFor), [typeof(IntVec3)]);
        var m_LabelDrawPosForOffset = AccessTools.Method(typeof(Patch_BeautyDrawer_DrawBeautyAroundMouse), nameof(LabelDrawPosForOffset));
        return instructions.MethodReplacer(m_LabelDrawPosFor, m_LabelDrawPosForOffset);
    }

    private static Vector2 LabelDrawPosForOffset(IntVec3 center)
    {
        Vector3 position = center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays).ToBaseMapCoord();
        Vector2 vector = Find.Camera.WorldToScreenPoint(position) / Prefs.UIScale;
        vector.y = UI.screenHeight - vector.y;
        vector.y -= 1f;
        return vector;
    }

    //FocusedVehicleをもとに戻しておく
    public static void Finalizer(object[] __state)
    {
        if (__state is not null)
        {
            Current.Game.currentMapIndex = (sbyte)__state[0];
            Command_FocusVehicleMap.FocusedVehicle = (VehiclePawnWithMap)__state[1];
        }
    }
}

//右下の温度表示
[HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
public static class Patch_GlobalControls_TemperatureString
{
    //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
    public static void Prefix(ref object[] __state)
    {
        Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
    }

    //FocusedVehicleをもとに戻しておく
    public static void Finalizer(object[] __state)
    {
        if (__state is not null)
        {
            Current.Game.currentMapIndex = (sbyte)__state[0];
            Command_FocusVehicleMap.FocusedVehicle = (VehiclePawnWithMap)__state[1];
        }
    }
}

//drawPosを移動してQuaternionに車の回転をかける
[HarmonyPatch]
public static class Patch_GUI_VehicleMapOffset
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GenUI), nameof(GenUI.RenderMouseoverBracket));
        yield return AccessTools.Method(typeof(DesignatorUtility), nameof(DesignatorUtility.RenderHighlightOverSelectableCells));
        yield return AccessTools.Method(typeof(Designator_Cancel), nameof(Designator_Cancel.RenderHighlight));
        yield return AccessTools.Method(typeof(CellBoolDrawer), "ActuallyDraw");
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity));
        codes.InsertAndAdvance(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1));
        codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
        codes.CreateLabelWithOffsets(1, out var label);
        codes.InsertAfter(
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedOnVehicleMap),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FullAngle),
                CodeInstruction.Call(typeof(Vector3Utility), nameof(Vector3Utility.FromAngleFlat)),
                CodeInstruction.Call(typeof(Quaternion), "op_Multiply", [typeof(Quaternion), typeof(Vector3)]));
        return codes.Instructions();
    }
}
