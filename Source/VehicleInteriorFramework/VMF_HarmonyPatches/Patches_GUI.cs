using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
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
                        Log.Error(string.Concat(new object[]
                        {
                            "Exception drawing ThingOverlay for ",
                            thing,
                            ": ",
                            ex
                        }));
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
            var getMaps = AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getMaps)) + 1;
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
        //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
        public static bool Prefix(ref VehiclePawnWithMap __state)
        {
            __state = Command_FocusVehicleMap.FocusedVehicle;
            if (Event.current.type != EventType.Repaint || Find.MainTabsRoot.OpenTab != null)
            {
                return false;
            }

            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
                if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
                {
                    Command_FocusVehicleMap.FocusedVehicle = __state;
                }
            }
            return true;
        }

        //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
        }

        //FocusedVehicleをもとに戻しておく
        public static void Finalizer(VehiclePawnWithMap __state)
        {
            Command_FocusVehicleMap.FocusedVehicle = __state;
        }
    }

    //Alt押した時のセル情報表示。MouseoverReadoutOnGUIと全く同じ
    [HarmonyPatch(typeof(CellInspectorDrawer), "DrawMapInspector")]
    public static class Patch_CellInspectorDrawer_DrawMapInspector
    {
        //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
        public static void Prefix(ref VehiclePawnWithMap __state)
        {
            __state = Command_FocusVehicleMap.FocusedVehicle;
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
                if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
                {
                    Command_FocusVehicleMap.FocusedVehicle = __state;
                }
            }
        }

        //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
        }

        //FocusedVehicleをもとに戻しておく
        public static void Finalizer(VehiclePawnWithMap __state)
        {
            Command_FocusVehicleMap.FocusedVehicle = __state;
        }
    }

    [HarmonyPatch(typeof(CellInspectorDrawer), nameof(CellInspectorDrawer.Update))]
    public static class Patch_CellInspectorDrawer_Update
    {
        //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
        public static void Prefix(ref VehiclePawnWithMap __state)
        {
            __state = Command_FocusVehicleMap.FocusedVehicle;
            if (!KeyBindingDefOf.ShowCellInspector.IsDown) return;
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
                if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
                {
                    Command_FocusVehicleMap.FocusedVehicle = __state;
                }
            }
        }

        //FocusedVehicleをもとに戻しておく
        public static void Finalizer(VehiclePawnWithMap __state)
        {
            Command_FocusVehicleMap.FocusedVehicle = __state;
        }
    }

    //Alt押した時のセルの美しさ
    [HarmonyPatch(typeof(BeautyDrawer), "DrawBeautyAroundMouse")]
    public static class Patch_BeautyDrawer_DrawBeautyAroundMouse
    {
        //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
        public static void Prefix(ref VehiclePawnWithMap __state)
        {
            __state = Command_FocusVehicleMap.FocusedVehicle;
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
                if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
                {
                    Command_FocusVehicleMap.FocusedVehicle = __state;
                }
            }
        }

        //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_LabelDrawPosFor = AccessTools.Method(typeof(GenMapUI), nameof(GenMapUI.LabelDrawPosFor), new[] { typeof(IntVec3) });
            var m_LabelDrawPosForOffset = AccessTools.Method(typeof(Patch_BeautyDrawer_DrawBeautyAroundMouse), nameof(LabelDrawPosForOffset));
            return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap)
                .MethodReplacer(m_LabelDrawPosFor, m_LabelDrawPosForOffset);
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
        public static void Finalizer(VehiclePawnWithMap __state)
        {
            Command_FocusVehicleMap.FocusedVehicle = __state;
        }
    }

    //右下の温度表示
    [HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
    public static class Patch_GlobalControls_TemperatureString
    {
        //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
        public static void Prefix(ref VehiclePawnWithMap __state)
        {
            __state = Command_FocusVehicleMap.FocusedVehicle;
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
                if (!UI.MouseCell().InBounds(vehicle.VehicleMap))
                {
                    Command_FocusVehicleMap.FocusedVehicle = __state;
                }
            }
        }

        //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
        }

        //FocusedVehicleをもとに戻しておく
        public static void Finalizer(VehiclePawnWithMap __state)
        {
            Command_FocusVehicleMap.FocusedVehicle = __state;
        }
    }
}
