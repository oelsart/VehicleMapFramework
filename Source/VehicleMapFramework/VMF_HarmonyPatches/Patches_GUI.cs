using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
[PatchLevel(Level.Sensitive)]
public static class Patch_SelectionDrawer_DrawSelectionOverlays
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var f_currentMapIndex = AccessTools.Field(typeof(Game), nameof(Game.currentMapIndex));
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(
        AccessTools.Method(typeof(List<object>), nameof(List<>.GetEnumerator))))
      .DeclareLocal(typeof(sbyte), out var mapIndex)
      .InsertAndAdvance(
        AccessTools.PropertyGetter(typeof(Current), nameof(Current.Game)).CallInstruction,
        new CodeInstruction(OpCodes.Ldfld, f_currentMapIndex),
        new CodeInstruction(OpCodes.Stloc_S, mapIndex))
      .MatchStartForward(CodeMatch.Calls(
        AccessTools.PropertyGetter(typeof(List<object>.Enumerator), nameof(List<>.Enumerator.Current))))
      .InsertAfterAndAdvance(
        new CodeInstruction(OpCodes.Dup),
        ((Delegate)FocusOnVehicleMap).Method.CallInstruction)
      .MatchStartForward(new CodeMatch(OpCodes.Endfinally))
      .Insert(
        AccessTools.PropertyGetter(typeof(Current), nameof(Current.Game)).CallInstruction,
        new CodeInstruction(OpCodes.Ldloc_S, mapIndex),
        new CodeInstruction(OpCodes.Stfld, f_currentMapIndex))
      .InstructionEnumeration();
  }
  
  private static void FocusOnVehicleMap(object obj)
  {
    var map = obj switch { Zone zone => zone.Map, Plan plan => plan.Map, Thing thing => thing.Map, _ => null };
    if (map is null)
      return;
    Current.Game.currentMapIndex = (sbyte)map.Index;
  }
}

//thingがIsOnVehicleMapだった場合回転の初期値num4にベースvehicleのAngleを与え、posはRotatePointで回転
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
[PatchLevel(Level.Safe)]
public static class Patch_SelectionDrawer_DrawSelectionBracketFor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 9);
    var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
    var label = generator.DefineLabel();

    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadLocal(2),
      new CodeInstruction(OpCodes.Ldloca_S, vehicle),
      CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf.CallInstruction,
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      CachedMethodInfo.m_FullAngle.CallInstruction,
      new CodeInstruction(OpCodes.Conv_I4),
      new CodeInstruction(OpCodes.Add),
    ]);

    var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 18);
    var label2 = generator.DefineLabel();

    codes[pos2].labels.Add(label2);
    codes.InsertRange(pos2,
    [
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      new CodeInstruction(OpCodes.Brfalse_S, label2),
      CodeInstruction.LoadLocal(2),
      CachedMethodInfo.g_Thing_DrawPos.CallvirtInstruction,
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      CachedMethodInfo.m_FullAngle.CallInstruction,
      new CodeInstruction(OpCodes.Neg),
      CachedMethodInfo.m_RotatePoint.CallInstruction
    ]);
    return codes;
  }
}

//VehicleMapはコロニストバーに表示させない
[HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ColonistBar_CheckRecacheEntries
{
  private static readonly AccessTools.FieldRef<MapPawns, Map> map = AccessTools.FieldRefAccess<MapPawns, Map>("map");

  private static readonly List<Pawn> tmpList = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps))))
      .InsertAfterAndAdvance(((Delegate)ExcludeVehicleMaps).Method.CallInstruction)
      .MatchStartForward(
        CodeMatch.Calls(AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.FreeColonists))))
      .Set(OpCodes.Call, ((Delegate)FreeColonists).Method)
      .InstructionEnumeration();
  }

  private static IEnumerable<Map> ExcludeVehicleMaps(this IEnumerable<Map> maps)
  {
    return maps?.Where(m => !m.IsVehicleMapOf(out var vehicle) || !vehicle.Spawned || m != vehicle.VehicleMap);
  }

  private static List<Pawn> FreeColonists(MapPawns instance)
  {
    var list = instance.FreeColonists;
    var baseMap = map(instance).GroundMap;
    for (var i = list.Count - 1; i >= 0; i--)
    {
      if (list[i].MapHeldBaseMap() != baseMap)
      {
        list.RemoveAt(i);
      }
    }

    return list;
  }
}

// コロニストバーに車両アイコンを表示するパッチ
[HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawGroupFrame))]
[PatchLevel(Level.Safe)]
public static class Patch_ColonistBarColonistDrawer_DrawGroupFrame
{
  public static void Postfix(int group)
  {
    var mode = VehicleMapFramework.settings.colonistBarMode;
    if (mode == VehicleMapSettings.ShowVehiclesOnColonistBar.DontShow)
      return;
    
    var colonistBar = Find.ColonistBar;
    Map map = null;
    foreach (var entry in colonistBar.Entries)
    {
      if (entry.group == group)
      {
        map = entry.map;
        break;
      }
    }
    if (map.IsVehicleMapOf(out var vehicle))
    {
      var rect = GroupFrameRect();
      if (mode == VehicleMapSettings.ShowVehiclesOnColonistBar.MouseIsOver && !rect.Contains(Event.current.mousePosition))
        return;
      
      var drawRect = new Rect(0f, rect.yMax - 5f, 50f, 50f);
      drawRect = drawRect.CenteredOnXIn(rect);
      var request = BlitRequest.For(vehicle.VehicleDef);
      VehicleGui.DrawVehicleOnGUI(drawRect, in request);
    }
    return;
    
    Rect GroupFrameRect()
    {
      const float BaseGroupFrameMargin = 12f;
      var num = 99999f;
      var num2 = 0f;
      var num3 = 0f;
      var entries = colonistBar.Entries;
      var drawLocs = colonistBar.DrawLocs;
      for (var i = 0; i < entries.Count; i++)
      {
        if (entries[i].group == group)
        {
          num = Mathf.Min(num, drawLocs[i].x);
          num2 = Mathf.Max(num2, drawLocs[i].x + colonistBar.Size.x);
          num3 = Mathf.Max(num3, drawLocs[i].y + colonistBar.Size.y);
        }
      }
      return new Rect(num, 0f, num2 - num, num3 - 0f).ContractedBy(-BaseGroupFrameMargin * colonistBar.Scale);
    }
  }
}

//左下のセル情報の表示。車両マップ上にマウスオーバーされている時はその車両マップの情報を表示する
[HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
[PatchLevel(Level.Safe)]
public static class Patch_MouseoverReadout_MouseoverReadoutOnGUI
{
  public static void PrefixCommon(ref sbyte? __state)
  {
    if (Command_FocusVehicleMap.FocusedVehicle is { } vehicle ||
        UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out vehicle))
    {
      __state = Current.Game.currentMapIndex;
      Current.Game.currentMapIndex = (sbyte)vehicle.CurrentLevel.Index;
    }
  }

  private static void Prefix(ref sbyte? __state)
  {
    if (Event.current.type != EventType.Repaint || Find.MainTabsRoot.OpenTab != null)
      return;

    PrefixCommon(ref __state);
  }

  public static void Finalizer(sbyte? __state)
  {
    if (__state is null) return;
    Current.Game.currentMapIndex = __state.Value;
  }
}

//Alt押した時のセル情報表示。MouseoverReadoutOnGUIと全く同じ
[HarmonyPatch(typeof(CellInspectorDrawer), "DrawMapInspector")]
[PatchLevel(Level.Safe)]
public static class Patch_CellInspectorDrawer_DrawMapInspector
{
  public static void Prefix(ref sbyte? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }

  public static void Finalizer(sbyte? __state) => Patch_MouseoverReadout_MouseoverReadoutOnGUI.Finalizer(__state);
}

[HarmonyPatch(typeof(CellInspectorDrawer), nameof(CellInspectorDrawer.Update))]
[PatchLevel(Level.Safe)]
public static class Patch_CellInspectorDrawer_Update
{
  public static void Prefix(ref sbyte? __state)
  {
    if (!KeyBindingDefOf.ShowCellInspector.IsDown) return;
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }
  
  public static void Finalizer(sbyte? __state) => Patch_MouseoverReadout_MouseoverReadoutOnGUI.Finalizer(__state);
}

[HarmonyPatch(typeof(EnvironmentStatsDrawer), nameof(EnvironmentStatsDrawer.DrawRoomOverlays))]
[PatchLevel(Level.Safe)]
public static class Patch_EnvironmentStatsDrawer_DrawRoomOverlays
{
  public static void Prefix(ref sbyte? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }
  
  public static void Finalizer(sbyte? __state) => Patch_MouseoverReadout_MouseoverReadoutOnGUI.Finalizer(__state);
}

//Alt押した時のセルの美しさ
[HarmonyPatch(typeof(BeautyDrawer), "DrawBeautyAroundMouse")]
public static class Patch_BeautyDrawer_DrawBeautyAroundMouse
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(ref sbyte? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_LabelDrawPosFor = ((Func<IntVec3, Vector2>)GenMapUI.LabelDrawPosFor).Method;
    var m_LabelDrawPosForOffset = ((Delegate)LabelDrawPosForOffset).Method;
    return instructions.MethodReplacer(m_LabelDrawPosFor, m_LabelDrawPosForOffset);
  }

  private static Vector2 LabelDrawPosForOffset(IntVec3 center)
  {
    var position = center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays).ToBaseMapCoord();
    Vector2 vector = Find.Camera.WorldToScreenPoint(position) / Prefs.UIScale;
    vector.y = UI.screenHeight - vector.y;
    vector.y -= 1f;
    return vector;
  }
  
  public static void Finalizer(sbyte? __state) => Patch_MouseoverReadout_MouseoverReadoutOnGUI.Finalizer(__state);
}

//右下の温度表示
[HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
[PatchLevel(Level.Safe)]
public static class Patch_GlobalControls_TemperatureString
{
  public static void Prefix(ref sbyte? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }
  
  public static void Finalizer(sbyte? __state) => Patch_MouseoverReadout_MouseoverReadoutOnGUI.Finalizer(__state);
}

//drawPosを移動してQuaternionに車の回転をかける
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_GUI_VehicleMapOffset
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return ((Delegate)GenUI.RenderMouseoverBracket).Method;
    yield return ((Delegate)DesignatorUtility.RenderHighlightOverSelectableCells).Method;
    yield return AccessTools.Method(typeof(Designator_Cancel), nameof(Designator_Cancel.RenderHighlight));
    yield return AccessTools.Method(typeof(CellBoolDrawer), "ActuallyDraw");
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity))
      .InsertAndAdvance(CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction)
      .Advance()
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .CreateLabel(out var label)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        CachedMethodInfo.m_FocusedOnVehicleMap.CallInstruction,
        new CodeInstruction(OpCodes.Brfalse_S, label))
      .MultiplyFullAngleQuat(vehicle)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.DesignationManagerOnGUI))]
[PatchLevel(Level.Safe)]
public static class Patch_DesignatorManager_DesignationManagerOnGUI
{
  internal static void Prefix([MustDisposeResource] ref FocusMapScope __state)
  {
    if (Command_FocusVehicleMap.FocusedVehicle is { } focused)
      __state = FocusMapScope.FocusMapUnsafe(focused.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

//v, v2にToBaseMapCoordをしてDrawBoxRotatedにFocusedVehicle.FullRotation.AsAngleを渡す
//Widgets.DrawNumberOnMap(screenPos, intVec.x, Color.white) ->
//Widgets.DrawNumberOnMap(ConvertToVehicleMap(screenPos), intVec.x, Color.white)を3回
[HarmonyBefore(DesignationsTooltip.HarmonyId)]
[HarmonyPatch(typeof(DesignationDragger), nameof(DesignationDragger.DraggerOnGUI))]
[PatchLevel(Level.Mandatory)]
public static class Patch_DesignationDragger_DraggerOnGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator, MethodBase original)
  {
    var codes = instructions.ToList();
    var c_Vector3 = AccessTools.Constructor(typeof(Vector3), [typeof(float), typeof(float), typeof(float)]);
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(c_Vector3)) + 1;
    var ind = original.GetMethodBody()!.LocalVariables.First(l => l.LocalType == typeof(Vector3)).LocalIndex;
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadLocal(ind),
      CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction,
      new CodeInstruction(OpCodes.Ldc_R4, 0f),
      CachedMethodInfo.m_Vector3Utility_WithY.CallInstruction,
      CodeInstruction.StoreLocal(ind)
    ]);

    var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Newobj && c.OperandIs(c_Vector3)) + 1;
    codes.InsertRange(pos2,
    [
      CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction,
      new CodeInstruction(OpCodes.Ldc_R4, 0f),
      CachedMethodInfo.m_Vector3Utility_WithY.CallInstruction,
    ]);

    var m_Widgets_DrawBox = ((Delegate)Widgets.DrawBox).Method;
    var pos3 = codes.FindIndex(pos2, c => c.Calls(m_Widgets_DrawBox));
    var m_DrawBoxRotated = ((Delegate)VMF_Widgets.DrawBoxRotated).Method;
    var label = generator.DefineLabel();
    var label2 = generator.DefineLabel();

    codes[pos3].operand = m_DrawBoxRotated;
    codes[pos3].labels.Add(label2);
    codes.InsertRange(pos3,
    [
      CachedMethodInfo.g_FocusedVehicle.CallInstruction,
      new CodeInstruction(OpCodes.Brfalse_S, label),
      CachedMethodInfo.g_FocusedVehicle.CallInstruction,
      CachedMethodInfo.m_ExtraAngle.CallInstruction,
      new CodeInstruction(OpCodes.Br_S, label2),
      new CodeInstruction(OpCodes.Ldc_R4, 0f).WithLabels(label),
    ]);

    var m_Widgets_DrawNumberOnMap = ((Delegate)Widgets.DrawNumberOnMap).Method;
    var m_ConvertToVehicleMap = ((Delegate)ConvertToVehicleMap).Method;
    var pos4 = codes.FindIndex(pos3, c => c.Calls(m_Widgets_DrawNumberOnMap)) - 3;
    codes.Insert(pos4, m_ConvertToVehicleMap.CallInstruction);

    var pos5 = codes.FindIndex(pos4 + 5, c => c.Calls(m_Widgets_DrawNumberOnMap)) - 3;
    codes.Insert(pos5, m_ConvertToVehicleMap.CallInstruction);

    var pos6 = codes.FindIndex(pos5 + 5, c => c.Calls(m_Widgets_DrawNumberOnMap));
    pos6 = codes.FindLastIndex(pos6, c => c.opcode == OpCodes.Ldarg_0);
    codes.Insert(pos6, m_ConvertToVehicleMap.CallInstruction);

    return codes;
  }

  private static Vector2 ConvertToVehicleMap(Vector2 screenPos)
  {
    screenPos.y = UI.screenHeight - screenPos.y;
    return UI.UIToMapPosition(screenPos).ToBaseMapCoord().Yto0().MapToUIPosition();
  }
}