using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework.VMF_HarmonyPatches;

//車上オブジェクトを選択
[HarmonyPatch(typeof(Selector), "SelectableObjectsUnderMouse")]
[PatchLevel(Level.Safe)]
public static class Patch_Selector_SelectableObjectsUnderMouse
{
    public static bool Prefix(ref IEnumerable<object> __result)
    {
        var mouseMapPosition = UI.MouseMapPosition();
        if (!mouseMapPosition.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.All))
        {
            return true;
        }
        __result = [.. SelectableObjects(vehicle, mouseMapPosition)];
        return !__result.Any();
    }

    private static IEnumerable<object> SelectableObjects(VehiclePawnWithMap vehicle, Vector3 mouseMapPosition)
    {
        TargetingParameters targetingParameters = new()
        {
            mustBeSelectable = true,
            canTargetPawns = true,
            canTargetBuildings = true,
            canTargetItems = true,
            mapObjectTargetsMustBeAutoAttackable = false
        };
        var mouseVehicleMapPosition = mouseMapPosition.ToVehicleMapCoord(vehicle);

        if (!mouseVehicleMapPosition.InBounds(vehicle.VehicleMap)) yield break;

        var selectableList = GenUIOnVehicle.ThingsUnderMouse(mouseVehicleMapPosition, 1f, targetingParameters, null, vehicle);
        if (selectableList.Count > 0)
        {
            if (selectableList[0] is Pawn && (selectableList[0].DrawPos - mouseMapPosition).MagnitudeHorizontal() < 0.4f)
            {
                for (var j = selectableList.Count - 1; j >= 0; j--)
                {
                    var thing2 = selectableList[j];
                    if (thing2.def.category == ThingCategory.Pawn && (thing2.DrawPosHeld!.Value - mouseMapPosition).MagnitudeHorizontal() > 0.4f)
                    {
                        selectableList.Remove(thing2);
                    }
                }
            }
        }

        foreach (var thing in selectableList)
        {
            yield return thing;
        }

        var zone = vehicle.CurrentLevel.zoneManager.ZoneAt(mouseVehicleMapPosition.ToIntVec3());
        if (zone != null)
        {
            yield return zone;
        }
        
        if (Find.CurrentMap == vehicle.VehicleMap && vehicle.Spawned) yield return vehicle;
    }
}

//選択したオブジェクトへのジャンプ時マップをVehicleMapからそのBaseMapに、cellはBaseMapの系に変換する
//Deselectの条件文のマップもBaseMapに変換
[HarmonyPatch(typeof(Selector), "SelectInternal")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Selector_SelectInternal
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
            .InsertAfterAndAdvance(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map));
        
        var l_intVec = codes.Instructions().Select(c => c.operand).OfType<LocalBuilder>()
            .First(l => l.LocalType == typeof(IntVec3));
        return codes
            .MatchStartForward(CodeMatch.IsStloc(l_intVec))
            .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
            .CreateLabel(out var label)
            .Insert(
                CodeInstruction.LoadLocal(3),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord3))
            .InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(CameraJumper), "TryJumpInternal", typeof(IntVec3), typeof(Map), typeof(CameraJumper.MovementMode))]
[PatchLevel(Level.Safe)]
public static class Patch_CameraJumper_TryJumpInternal
{
    public static void Prefix(ref IntVec3 cell, ref Map map)
    {
        if (map.IsVehicleMapOf(out var vehicle))
        {
            if (MultiFloors.Active)
            {
                vehicle.CurrentLevel = map;
            }

            if (VehicleMapFramework.settings.drawPlanet)
            {
                if (vehicle.Spawned)
                {
                    map = vehicle.Map;
                    cell = cell.ToBaseMapCoord(vehicle);
                    return;
                }
                cell = cell.ToBaseMapCoord(vehicle);
                Patch_Map_MapUpdate.lastRenderedTick = -1;
            }
        }
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.CurrentMap), MethodType.Setter)]
[PatchLevel(Level.Safe)]
public static class Patch_Game_CurrentMap
{
    public static bool ForceSet { get; set; }
    
    public static void Prefix(ref Map value)
    {
        if (ForceSet)
        {
            ForceSet = false;
            return;
        }
        if (value.IsVehicleMapOf(out var vehicle))
        {
            if (MultiFloors.Active)
            {
                vehicle.CurrentLevel = value;
            }
            if (vehicle.Spawned)
            {
                value = vehicle.Map;
            }
            else if (VehicleMapFramework.settings.drawPlanet)
            {
                Patch_Map_MapUpdate.lastRenderedTick = -1;
            }
        }
    }
}

//フォーカスしたVehicleがある場合それ用の改変メソッドを呼んでオリジナルをスキップ
[HarmonyPatch(typeof(ThingSelectionUtility), "MultiSelectableThingsInScreenRectDistinct")]
[PatchLevel(Level.Safe)]
public static class Patch_ThingSelectionUtility_MultiSelectableThingsInScreenRectDistinct
{
    private static readonly FastInvokeHandler SelectableByMapClick = MethodInvoker.GetHandler(AccessTools.Method(typeof(ThingSelectionUtility), "SelectableByMapClick"));

    private static readonly HashSet<Thing> yieldedThings = [];
    public static bool Prefix(ref IEnumerable<object> __result, Rect rect)
    {
        var mouseMapPosition = UI.MouseMapPosition();
        if (!mouseMapPosition.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.All))
        {
            return true;
        }
        __result = MultiSelectableThings(vehicle, rect);
        return !__result.Any();
    }

    private static IEnumerable<object> MultiSelectableThings(VehiclePawnWithMap vehicle, Rect rect)
    {
        var focusedMap = vehicle.VehicleMap;
        var mapRect = GetMapRect(rect);
        yieldedThings.Clear();
        foreach (var cellThings in from c in mapRect
                 select c.ToVehicleMapCoord(vehicle)
                 into c2
                 where c2.InBounds(focusedMap)
                 select focusedMap.thingGrid.ThingsListAt(c2)
                 into cellThings
                 where cellThings != null
                 select cellThings)
        {
            int num;
            for (var i = 0; i < cellThings.Count; i = num + 1)
            {
                var t = cellThings[i];
                if ((bool)SelectableByMapClick(null, t) && !t.def.neverMultiSelect)
                {
                    yieldedThings.Add(t);
                }
                num = i;
            }
        }
        var rectInWorldSpace = GetRectInWorldSpace(rect);
        foreach (var c2 in mapRect.ExpandedBy(1).EdgeCells)
        {
            var c3 = c2.ToVehicleMapCoord(vehicle);
            if (c3.InBounds(focusedMap) && c3.GetItemCount(focusedMap) > 1)
            {
                foreach (var t in focusedMap.thingGrid.ThingsAt(c3))
                {
                    if (t.def.category == ThingCategory.Item && (bool)SelectableByMapClick(null, t) && !t.def.neverMultiSelect && !yieldedThings.Contains(t))
                    {
                        var vector = t.TrueCenter();
                        Rect rect2 = new(vector.x - 0.5f, vector.z - 0.5f, 1f, 1f);
                        if (rect2.Overlaps(rectInWorldSpace))
                        {
                            yieldedThings.Add(t);
                        }
                    }
                }
            }
        }

        return yieldedThings;
    }

    private static CellRect GetMapRect(Rect rect)
    {
        Vector2 screenLoc = new(rect.x, UI.screenHeight - rect.y);
        Vector2 screenLoc2 = new(rect.x + rect.width, UI.screenHeight - (rect.y + rect.height));
        var vector = UI.UIToMapPosition(screenLoc);
        var vector2 = UI.UIToMapPosition(screenLoc2);
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
        Vector2 screenLoc = new(rect.x, UI.screenHeight - rect.y);
        Vector2 screenLoc2 = new(rect.x + rect.width, UI.screenHeight - (rect.y + rect.height));
        var vector = UI.UIToMapPosition(screenLoc);
        var vector2 = UI.UIToMapPosition(screenLoc2);
        return new Rect(vector.x, vector2.z, vector2.x - vector.x, vector.z - vector2.z);
    }
}
