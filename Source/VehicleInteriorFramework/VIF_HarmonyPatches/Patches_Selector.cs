using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch]
    public static class Patch_Selector_SelectableObjectsUnderMouse_MoveNext
    {
        private static MethodInfo TargetMethod()
        {
            return AccessTools.InnerTypes(typeof(Selector)).Where(t => t.Name.Contains("SelectableObjectsUnderMouse")).SelectMany(t => t.GetMethods(AccessTools.all)).First(m => m.Name == "MoveNext");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_UI_MouseCell = AccessTools.Method(typeof(UI), nameof(UI.MouseCell));
            var m_Stub_MouseCell = AccessTools.Method(typeof(Patch_UI_MouseCell), nameof(Patch_UI_MouseCell.MouseCell));
            return instructions.MethodReplacer(m_UI_MouseCell, m_Stub_MouseCell);
        }
    }

    //車上オブジェクトを選択
    [HarmonyPatch(typeof(Selector), "SelectableObjectsUnderMouse")]
    public static class Patch_Selector_SelectableObjectsUnderMouse
    {
        public static void Postfix(ref IEnumerable<object> __result)
        {
            var mouseMapPosition = UI.MouseMapPosition();
            if (mouseMapPosition.TryGetVehiclePawnWithMap(Find.CurrentMap, out var vehicle))
            {
                TargetingParameters targetingParameters = new TargetingParameters
                {
                    mustBeSelectable = true,
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetItems = true,
                    mapObjectTargetsMustBeAutoAttackable = false
                };
                var mouseVehicleMapPosition = mouseMapPosition.VehicleMapToOrig(vehicle);

                if (!mouseVehicleMapPosition.InBounds(vehicle.VehicleMap)) return;

                List<Thing> selectableList = GenUIOnVehicle.ThingsUnderMouse(mouseMapPosition, 1f, targetingParameters, null, vehicle);
                if (selectableList.Count > 0)
                {
                    __result = __result.Except(vehicle);
                    if (selectableList[0] is Pawn && (selectableList[0].DrawPos - mouseMapPosition).MagnitudeHorizontal() < 0.4f)
                    {
                        for (int j = selectableList.Count - 1; j >= 0; j--)
                        {
                            Thing thing2 = selectableList[j];
                            if (thing2.def.category == ThingCategory.Pawn && (thing2.DrawPosHeld.Value - mouseMapPosition).MagnitudeHorizontal() > 0.4f)
                            {
                                selectableList.Remove(thing2);
                            }
                        }
                    }
                }

                foreach (var thing in selectableList)
                {
                    __result = __result.AddItem(thing);
                }

                Zone zone = vehicle.VehicleMap.zoneManager.ZoneAt(mouseVehicleMapPosition.ToIntVec3());
                if (zone != null)
                {
                    __result = __result.AddItem(zone);
                }
            }
        }
    }

    //選択したオブジェクトへのジャンプ時マップをVehicleMapからそのBaseMapに、cellはBaseMapの系に変換する
    //Deselectの条件文のマップもBaseMapに変換
    [HarmonyPatch(typeof(Selector), "SelectInternal")]
    public static class Patch_Selector_SelectInternal
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap)
                .MethodReplacer(MethodInfoCache.g_Zone_Map, MethodInfoCache.m_BaseMap_Zone).ToList();
            var g_Zone_Cells = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.Cells));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(g_Zone_Cells));
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Br_S);
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(1),
                new CodeInstruction(OpCodes.Castclass, typeof(Zone)),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Zone_Map),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Spawned),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2)
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 6);
            var label2 = codes[pos2].labels[0];
            var vehicle2 = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes.InsertRange(pos2, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle2),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle2),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2)
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(CameraJumper), "TryJumpInternal", typeof(IntVec3), typeof(Map), typeof(CameraJumper.MovementMode))]
    public static class Patch_CameraJumper_TryJumpInternal
    {
        public static void Prefix(ref IntVec3 cell, ref Map map)
        {
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                cell = cell.OrigToVehicleMap(vehicle);
                map = vehicle.Map;
            }
        }
    }

    //フォーカスしたVehicleがある場合それ用の改変メソッドを呼んでオリジナルをスキップ
    [HarmonyPatch(typeof(ThingSelectionUtility), "MultiSelectableThingsInScreenRectDistinct")]
    public static class Patch_ThingSelectionUtility_MultiSelectableThingsInScreenRectDistinct
    {
        public static bool Prefix(ref IEnumerable<object> __result, Rect rect)
        {
            if (Command_FocusVehicleMap.FocusedVehicle == null) return true;

            var focusedMap = Command_FocusVehicleMap.FocusedVehicle.VehicleMap;
            __result = new List<object>();
            CellRect mapRect = GetMapRect(rect);
            yieldedThings.Clear();
            try
            {
                foreach (IntVec3 c in mapRect)
                {
                    var c2 = c.VehicleMapToOrig(Command_FocusVehicleMap.FocusedVehicle);
                    if (c2.InBounds(focusedMap))
                    {
                        List<Thing> cellThings = focusedMap.thingGrid.ThingsListAt(c2);
                        if (cellThings != null)
                        {
                            int num;
                            for (int i = 0; i < cellThings.Count; i = num + 1)
                            {
                                Thing t = cellThings[i];
                                if ((bool)SelectableByMapClick(null, t) && !t.def.neverMultiSelect && !yieldedThings.Contains(t))
                                {
                                    __result = __result.AddItem(t);
                                    yieldedThings.Add(t);
                                }
                                num = i;
                            }
                        }
                    }
                }
                Rect rectInWorldSpace = GetRectInWorldSpace(rect);
                foreach (IntVec3 c2 in mapRect.ExpandedBy(1).EdgeCells)
                {
                    var c3 = c2.VehicleMapToOrig(Command_FocusVehicleMap.FocusedVehicle);
                    if (c3.InBounds(focusedMap) && c3.GetItemCount(focusedMap) > 1)
                    {
                        foreach (Thing t in focusedMap.thingGrid.ThingsAt(c3))
                        {
                            if (t.def.category == ThingCategory.Item && (bool)SelectableByMapClick(null, t) && !t.def.neverMultiSelect && !yieldedThings.Contains(t))
                            {
                                Vector3 vector = t.TrueCenter().OrigToVehicleMap(Command_FocusVehicleMap.FocusedVehicle);
                                Rect rect2 = new Rect(vector.x - 0.5f, vector.z - 0.5f, 1f, 1f);
                                if (rect2.Overlaps(rectInWorldSpace))
                                {
                                    __result = __result.AddItem(t);
                                    yieldedThings.Add(t);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                yieldedThings.Clear();
            }
            return false;
        }

        private static CellRect GetMapRect(Rect rect)
        {
            Vector2 screenLoc = new Vector2(rect.x, (float)UI.screenHeight - rect.y);
            Vector2 screenLoc2 = new Vector2(rect.x + rect.width, (float)UI.screenHeight - (rect.y + rect.height));
            Vector3 vector = UI.UIToMapPosition(screenLoc);
            Vector3 vector2 = UI.UIToMapPosition(screenLoc2);
            return new CellRect
            {
                minX = Mathf.FloorToInt(vector.x),
                minZ = Mathf.FloorToInt(vector2.z),
                maxX = Mathf.FloorToInt(vector2.x),
                maxZ = Mathf.FloorToInt(vector.z)
            };
        }

        private static Rect GetRectInWorldSpace(Rect rect)
        {
            Vector2 screenLoc = new Vector2(rect.x, (float)UI.screenHeight - rect.y);
            Vector2 screenLoc2 = new Vector2(rect.x + rect.width, (float)UI.screenHeight - (rect.y + rect.height));
            Vector3 vector = UI.UIToMapPosition(screenLoc);
            Vector3 vector2 = UI.UIToMapPosition(screenLoc2);
            return new Rect(vector.x, vector2.z, vector2.x - vector.x, vector.z - vector2.z);
        }

        private static readonly FastInvokeHandler SelectableByMapClick = MethodInvoker.GetHandler(AccessTools.Method(typeof(ThingSelectionUtility), "SelectableByMapClick"));

        private static readonly HashSet<Thing> yieldedThings = AccessTools.StaticFieldRefAccess<HashSet<Thing>>(typeof(ThingSelectionUtility), "yieldedThings");
    }
}
