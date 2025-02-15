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
            codes.Insert(pos, CodeInstruction.Call(typeof(Patch_ColonistBar_CheckRecacheEntries), nameof(Patch_ColonistBar_CheckRecacheEntries.ExcludeVehicleMaps)));
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
        public static void Prefix(ref VehiclePawnWithMap __state)
        {
            __state = Command_FocusVehicleMap.FocusedVehicle;
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
            {
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
            }
        }

        //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Find_CurrentMap, MethodInfoCache.g_VehicleMapUtility_CurrentMap);
        }

        //FocusedVehicleをもとに戻しておく
        public static void Postfix(VehiclePawnWithMap __state)
        {
            Command_FocusVehicleMap.FocusedVehicle = __state;
        }
    }
}
