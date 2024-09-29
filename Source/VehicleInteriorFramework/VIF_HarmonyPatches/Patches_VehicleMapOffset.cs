using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    //フォーカスしたVehicleがある場合それ用の改変メソッドを呼んでオリジナルをスキップ
    [HarmonyPatch(typeof(Selector), "SelectableObjectsUnderMouse")]
    public static class Patch_Selector_SelectableObjectsUnderMouse
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Map_MapUpdate.Transpiler(instructions);
        }

        public static bool Prefix(ref IEnumerable<object> __result)
        {
            if (VehicleMapUtility.FocusedVehicle == null) return true;

            __result = new List<object>();
            Vector2 mousePositionOnUIInverted = UI.MousePositionOnUIInverted;
            Thing thing = Find.ColonistBar.ColonistOrCorpseAt(mousePositionOnUIInverted);
            if (thing != null && thing.SpawnedOrAnyParentSpawned)
            {
                __result = __result.AddItem(thing);
                return false;
            }
            if (!UI.MouseCell().InBounds(Find.CurrentMap))
            {
                return false;
            }
            TargetingParameters targetingParameters = new TargetingParameters();
            targetingParameters.mustBeSelectable = true;
            targetingParameters.canTargetPawns = true;
            targetingParameters.canTargetBuildings = true;
            targetingParameters.canTargetItems = true;
            targetingParameters.mapObjectTargetsMustBeAutoAttackable = false;
            var mouseVehicleMapPosition = UI.MouseMapPosition().VehicleMapToOrig();
            List<Thing> selectableList = GenUI.ThingsUnderMouse(mouseVehicleMapPosition, 1f, targetingParameters, null);
            if (selectableList.Count > 0 && selectableList[0] is Pawn && (selectableList[0].DrawPos - mouseVehicleMapPosition).MagnitudeHorizontal() < 0.4f)
            {
                for (int j = selectableList.Count - 1; j >= 0; j--)
                {
                    Thing thing2 = selectableList[j];
                    if (thing2.def.category == ThingCategory.Pawn && (thing2.DrawPosHeld.Value - mouseVehicleMapPosition).MagnitudeHorizontal() > 0.4f)
                    {
                        selectableList.Remove(thing2);
                    }
                }
            }
            int num;
            for (int i = 0; i < selectableList.Count; i = num + 1)
            {
                __result = __result.AddItem(selectableList[i]);
                num = i;
            }
            Zone zone = Find.CurrentMap.zoneManager.ZoneAt(UI.MouseCell());
            if (zone != null)
            {
                __result = __result.AddItem(zone);
            }
            return false;
        }
    }

    //フォーカスしたVehicleがある場合それ用の改変メソッドを呼んでオリジナルをスキップ
    [HarmonyPatch(typeof(ThingSelectionUtility), "MultiSelectableThingsInScreenRectDistinct")]
    public static class Patch_ThingSelectionUtility_MultiSelectableThingsInScreenRectDistinct
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Map_MapUpdate.Transpiler(instructions);
        }

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

    [HarmonyPatch(typeof(UI), nameof(UI.MouseCell))]
    public static class Patch_UI_MouseCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var toIntVec3 = AccessTools.Method(typeof(IntVec3Utility), nameof(IntVec3Utility.ToIntVec3));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(toIntVec3));
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, VehicleMapUtility.m_VehicleMapToOrig1));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawPos), MethodType.Getter)]
    public static class Patch_Thing_DrawPos
    {
        public static bool Prefix(Thing __instance, ref Vector3 __result)
        {
            if (!OnVehiclePositionCache.cacheMode && OnVehiclePositionCache.cachedDrawPos.TryGetValue(__instance, out var pos))
            {
                __result = pos;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    public static class Patch_Pawn_DrawPos
    {
        public static bool Prefix(Pawn __instance, ref Vector3 __result)
        {
            if (!OnVehiclePositionCache.cacheMode && OnVehiclePositionCache.cachedDrawPos.TryGetValue(__instance, out var pos))
            {
                __result = pos;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DrawPos), MethodType.Getter)]
    public static class Patch_VehiclePawn_DrawPos
    {
        public static bool Prefix(VehiclePawn __instance, ref Vector3 __result)
        {
            if (!OnVehiclePositionCache.cacheMode && OnVehiclePositionCache.cachedDrawPos.TryGetValue(__instance, out var pos))
            {
                __result = pos;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AttachableThing), nameof(AttachableThing.DrawPos), MethodType.Getter)]
    public static class Patch_AttachableThing_DrawPos
    {
        public static bool Prefix(AttachableThing __instance, ref Vector3 __result)
        {
            if (!OnVehiclePositionCache.cacheMode && OnVehiclePositionCache.cachedDrawPos.TryGetValue(__instance, out var pos))
            {
                __result = pos;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MechShield), nameof(MechShield.DrawPos), MethodType.Getter)]
    public static class Patch_MechShield_DrawPos
    {
        public static bool Prefix(MechShield __instance, ref Vector3 __result)
        {
            if (!OnVehiclePositionCache.cacheMode && OnVehiclePositionCache.cachedDrawPos.TryGetValue(__instance, out var pos))
            {
                __result = pos;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Mote), nameof(Mote.DrawPos), MethodType.Getter)]
    public static class Patch_Mote_DrawPos
    {
        public static bool Prefix(Mote __instance, ref Vector3 __result)
        {
            if (!OnVehiclePositionCache.cacheMode && OnVehiclePositionCache.cachedDrawPos.TryGetValue(__instance, out var pos))
            {
                __result = pos;
                return false;
            }
            return true;
        }
    }

    //描画位置をOrigToVehicleMapで調整して回転はextraRotationに渡す
    [HarmonyPatch(typeof(GhostDrawer), nameof(GhostDrawer.DrawGhostThing))]
    public static class Patch_GhostDrawer_DrawGhostThing
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var getTrueCenter = AccessTools.Method(typeof(GenThing), nameof(GenThing.TrueCenter), new Type[] { typeof(IntVec3), typeof(Rot4), typeof(IntVec2), typeof(float) });
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getTrueCenter)) + 1;
            codes.Insert(pos, CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.OrigToVehicleMap), new Type[] { typeof(Vector3) }));

            var label = generator.DefineLabel();
            var drawFromDef = AccessTools.Method(typeof(Graphic), nameof(Graphic.DrawFromDef));
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(drawFromDef));
            var rot = generator.DeclareLocal(typeof(Rot8));
            codes[pos2].labels.Add(label);
            codes.InsertRange(pos2, new[]
            {
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedVehicle))),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(VehicleMapUtility), nameof(VehicleMapUtility.FocusedVehicle))),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(VehiclePawnWithInterior), nameof(VehiclePawnWithInterior.FullRotation))),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle))),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }

    //thingのMapのParentがVehicleMapだった場合回転の初期値であるnum3にvehicleの回転を与える
    [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
    public static class Patch_SelectionDrawer_DrawGhostThing
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && (c.operand as LocalBuilder).LocalIndex == 8);
            var label = generator.DefineLabel();
            var parent = generator.DeclareLocal(typeof(MapParent_Vehicle));
            var rot = generator.DeclareLocal(typeof(Rot8));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(1),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map))),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Map), nameof(Map.Parent))),
                new CodeInstruction(OpCodes.Isinst, typeof(MapParent_Vehicle)),
                new CodeInstruction(OpCodes.Stloc_S, parent),
                new CodeInstruction(OpCodes.Ldloc_S, parent),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, parent),
                CodeInstruction.LoadField(typeof(MapParent_Vehicle), nameof(MapParent_Vehicle.vehicle)),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(VehiclePawnWithInterior), nameof(VehiclePawnWithInterior.FullRotation))),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.AsAngle))),
                new CodeInstruction(OpCodes.Conv_I4),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(DynamicDrawManager), "ComputeCulledThings")]
    public static class Patch_DynamicDrawManager_ComputeCulledThings
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var expandedBy = AccessTools.Method(typeof(CellRect), nameof(CellRect.ExpandedBy));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(expandedBy)) + 1;
            var label = generator.DefineLabel();
            var parentVehicle = generator.DeclareLocal(typeof(MapParent_Vehicle));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[] {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(DynamicDrawManager), "map"),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Map), nameof(Map.Parent))),
                new CodeInstruction(OpCodes.Isinst, typeof(MapParent_Vehicle)),
                new CodeInstruction(OpCodes.Stloc_S, parentVehicle),
                new CodeInstruction(OpCodes.Ldloc_S, parentVehicle),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, parentVehicle),
                CodeInstruction.LoadField(typeof(MapParent_Vehicle), nameof(MapParent_Vehicle.vehicle)),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.VehicleMapToOrig), new Type[]{ typeof(CellRect), typeof(VehiclePawnWithInterior) }))
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(Graphic), nameof(Graphic.Draw))]
    public static class Patch_Graphic_Draw
    {
        public static void Prefix(Thing thing, ref float extraRotation)
        {
            if (thing.Map.Parent is MapParent_Vehicle parentVehicle)
            {
                extraRotation += parentVehicle.vehicle.FullRotation.AsAngle;
            }
        }
    }

    [HarmonyPatch(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI))]
    public static class Patch_ThingOverlays_ThingOverlaysOnGUI
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var from = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
            var to = AccessTools.Method(typeof(Patch_Find_CurrentMap), nameof(Patch_Find_CurrentMap.CurrentMap));
            var codes = instructions.MethodReplacer(from, to).MethodReplacer(VehicleMapUtility.m_Thing_Position, VehicleMapUtility.m_PositionOnBaseMap).ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);

            codes.Insert(pos, CodeInstruction.Call(typeof(Patch_ThingOverlays_ThingOverlaysOnGUI), nameof(Patch_ThingOverlays_ThingOverlaysOnGUI.IncludeVehicleMapThings)));

            return codes;
        }

        public static List<Thing> IncludeVehicleMapThings(List<Thing> list)
        {
            var vehicles = list.OfType<VehiclePawnWithInterior>();
            var result = new List<Thing>(list);
            foreach (var vehicle in vehicles)
            {
                result.AddRange(vehicle.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay));
            }
            return result;
        }
    }
}
