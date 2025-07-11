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

[HarmonyPatch]
public static class Patch_GUI_FocusVehicleMap
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI));
        yield return AccessTools.Method(typeof(CellInspectorDrawer), "DrawMapInspector");
        yield return AccessTools.Method(typeof(BeautyDrawer), "DrawBeautyAroundMouse");
        yield return AccessTools.Method(typeof(GlobalControls), "TemperatureString");
    }

    //車両マップにマウスオーバーしていたらCurrentMapに入れておく。これでMouseCellが勝手にオフセットされる
    public static bool Prefix(ref sbyte __state)
    {
        __state = (sbyte)Find.CurrentMap.Index;
        if (Event.current.type != EventType.Repaint || Find.MainTabsRoot.OpenTab != null)
        {
            return false;
        }

        if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
        {
            Current.Game.currentMapIndex = (sbyte)vehicle.VehicleMap.Index;
            if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
            {
                Current.Game.currentMapIndex = __state;
            }
        }
        return true;
    }

    //CurrentMapをもとに戻しておく
    public static void Finalizer(sbyte __state)
    {
        Current.Game.currentMapIndex = (sbyte)__state;
    }
}

//Alt押した時のセルの美しさ
[HarmonyPatch(typeof(BeautyDrawer), "DrawBeautyAroundMouse")]
public static class Patch_BeautyDrawer_DrawBeautyAroundMouse
{
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
        codes.InsertAndAdvance(
            [
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord1),
            new CodeInstruction(OpCodes.Ldc_R4, AltitudeLayer.MetaOverlays.AltitudeFor()),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_Vector3Utility_WithY)
            ]);

        codes.CreateLabelWithOffsets(1, out var label);
        codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
        codes.InsertAfter(
            [
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedOnVehicleMap),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_FullRotation),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_Rot8_AsQuat),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply),
            ]);
        return codes.Instructions();
    }
}
