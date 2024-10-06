using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    //フォーカスしたVehicleがある場合それ用の改変メソッドを呼んでオリジナルをスキップ
    [HarmonyPatch(typeof(Selector), "SelectableObjectsUnderMouse")]
    public static class Patch_Selector_SelectableObjectsUnderMouse
    {
        public static void Postfix(ref IEnumerable<object> __result)
        {
            VehiclePawnWithInterior vehicle = null;
            if (__result.Any(o => (vehicle = o as VehiclePawnWithInterior) != null))
            {
                TargetingParameters targetingParameters = new TargetingParameters();
                targetingParameters.mustBeSelectable = true;
                targetingParameters.canTargetPawns = true;
                targetingParameters.canTargetBuildings = true;
                targetingParameters.canTargetItems = true;
                targetingParameters.mapObjectTargetsMustBeAutoAttackable = false;
                var mouseMapPosition = UI.MouseMapPosition();
                var mouseVehicleMapPosition = mouseMapPosition.VehicleMapToOrig(vehicle);

                if (!mouseVehicleMapPosition.InBounds(vehicle.interiorMap)) return;

                List<Thing> selectableList = SelectorOnVehicleUtility.ThingsUnderMouse(mouseMapPosition, 1f, targetingParameters, null, vehicle);
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

                Zone zone = vehicle.interiorMap.zoneManager.ZoneAt(mouseVehicleMapPosition.ToIntVec3());
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
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_3);
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithInterior));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Map),
            });

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S) - 1;
            var label2 = generator.DefineLabel();
            var vehicle2 = generator.DeclareLocal(typeof(VehiclePawnWithInterior));

            var code = codes[pos2];
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Dup).MoveLabelsFrom(codes[pos2]),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle2),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label2),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle2),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Map),
            });
            code.labels.Add(label2);

            var pos3 = codes.FindIndex(pos2, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 6);
            var label4 = generator.DefineLabel();

            codes[pos3].labels.Add(label4);
            codes.InsertRange(pos3, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label4),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_OrigToVehicleMap2)
            });
            return codes;
        }
    }

    /*[HarmonyPatch(typeof(CameraJumper), "TryJumpInternal", typeof(IntVec3), typeof(Map), typeof(CameraJumper.MovementMode))]
    public static class Patch_CameraJumper_TryJumpInternal
    {
        public static void Prefix(ref IntVec3 cell, ref Map map)
        {
            if (map.IsVehicleMapOf(out var vehicle))
            {
                cell = cell.OrigToVehicleMap(vehicle);
                map = vehicle.Map;
            }
        }
    }*/

    //フォーカスしたVehicleがある場合それ用の改変メソッドを呼んでオリジナルをスキップ
    [HarmonyPatch(typeof(ThingSelectionUtility), "MultiSelectableThingsInScreenRectDistinct")]
    public static class Patch_ThingSelectionUtility_MultiSelectableThingsInScreenRectDistinct
    {
        public static bool Prefix(ref IEnumerable<object> __result, Rect rect)
        {
            if (VehicleMapUtility.FocusedVehicle == null) return true;

            __result = new List<object>();
            CellRect mapRect = GetMapRect(rect);
            yieldedThings.Clear();
            try
            {
                foreach (IntVec3 c in mapRect)
                {
                    if (c.InBounds(Find.CurrentMap))
                    {
                        List<Thing> cellThings = Find.CurrentMap.thingGrid.ThingsListAt(c);
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
                    if (c2.InBounds(Find.CurrentMap) && c2.GetItemCount(Find.CurrentMap) > 1)
                    {
                        foreach (Thing t in Find.CurrentMap.thingGrid.ThingsAt(c2))
                        {
                            if (t.def.category == ThingCategory.Item && (bool)SelectableByMapClick(null, t) && !t.def.neverMultiSelect && !yieldedThings.Contains(t))
                            {
                                Vector3 vector = t.TrueCenter();
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
            Vector3 vector = UI.UIToMapPosition(screenLoc).VehicleMapToOrig();
            Vector3 vector2 = UI.UIToMapPosition(screenLoc2).VehicleMapToOrig();
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
            Vector3 vector = UI.UIToMapPosition(screenLoc).VehicleMapToOrig();
            Vector3 vector2 = UI.UIToMapPosition(screenLoc2).VehicleMapToOrig();
            return new Rect(vector.x, vector2.z, vector2.x - vector.x, vector.z - vector2.z);
        }

        private static readonly FastInvokeHandler SelectableByMapClick = MethodInvoker.GetHandler(AccessTools.Method(typeof(ThingSelectionUtility), "SelectableByMapClick"));

        private static readonly HashSet<Thing> yieldedThings = AccessTools.StaticFieldRefAccess<HashSet<Thing>>(typeof(ThingSelectionUtility), "yieldedThings");
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class Patch_FloatMenuMakerMap_AddHumanlikeOrders
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddJobGiverWorkOrders")]
    public static class Patch_FloatMenuMakerMap_AddJobGiverWorkOrders
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
    public static class Patch_FloatMenuMakerMap_ChoicesAtFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddDraftedOrders")]
    public static class Patch_FloatMenuMakerMap_AddDraftedOrders
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(FloatMenuMakerMap), "GotoLocationOption"),
                AccessTools.Method(typeof(FloatMenuMakerOnVehicle), nameof(FloatMenuMakerOnVehicle.GotoLocationOption)));
        }
    }

    /*[HarmonyPatch(typeof(RCellFinder), nameof(RCellFinder.BestOrderedGotoDestNear))]
    public static class ReaversePatch_RCellFinder_BestOrderedGotoDestNear
    {
        [HarmonyReversePatch]
        public static IntVec3 BestOrderedGotoDestNear(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, Map map)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map)) - 2;

                codes.RemoveRange(pos, 3);
                codes.Insert(pos, CodeInstruction.LoadArgument(3));
                return codes;
            }
            _ = Transpiler(null);
            throw new NotImplementedException();
        }
    }*/

    [HarmonyPatch]
    public static class Patch_EnterPortalUtility_GetFloatMenuOptFor
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(Pawn), typeof(IntVec3))]
        public static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(List<Pawn>), typeof(IntVec3))]
        public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMapOfThing);
        }
    }
}
