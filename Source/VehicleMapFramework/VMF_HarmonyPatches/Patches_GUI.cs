using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI))]
[PatchLevel(Level.Safe)]
public static class Patch_ThingOverlays_ThingOverlaysOnGUI
{
  public static bool Prefix()
  {
    if (Event.current.type != EventType.Repaint) return true;
    var bounds = Find.CameraDriver.CurrentViewRect.ToBounds();
    var flag = Find.CurrentMap.IsVehicleMapOf(out var vehicle);
    var vehicles = flag ? GetVehicles() : VehiclePawnWithMapCache.AllVehiclesOn(Find.CurrentMap);
    foreach (var vehicle2 in vehicles)
    {
      if (!flag && bounds.Contains(vehicle2.DrawPos.Yto0()))
      {
        try
        {
          vehicle2.DrawGUIOverlay();
        }
        catch (Exception ex)
        {
          Log.Error($"Exception drawing ThingOverlay for {vehicle2}: {ex}");
        }
      }

      foreach (var thing in vehicle2.CurrentLevel.listerThings.ThingsInGroup(ThingRequestGroup.HasGUIOverlay))
      {
        if (bounds.Contains(thing.DrawPos.Yto0()) /* && !Find.CurrentMap.fogGrid.IsFogged(thing.PositionOnBaseMap)*/
           ) //車両マップである時点でFoggedはスキップしていいはず
        {
          try
          {
            thing.DrawGUIOverlay();
          }
          catch (Exception ex)
          {
            Log.Error($"Exception drawing ThingOverlay for {thing}: {ex}");
          }
        }
      }
    }

    return !flag;

    IEnumerable<VehiclePawnWithMap> GetVehicles()
    {
      if (vehicle.VehicleCaravanOrStashedVehicle is { } vehicleCaravanOrStashedVehicle)
      {
        foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles.OfType<VehiclePawnWithMap>())
          yield return vehicle2;
      }
      else
      {
        yield return vehicle;
      }
    }
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
    return new CodeMatcher(instructions)
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

//左下のセル情報の表示。車両マップ上にマウスオーバーされている時はその車両マップの情報を表示する
[HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
[PatchLevel(Level.Safe)]
public static class Patch_MouseoverReadout_MouseoverReadoutOnGUI
{
  public static void PrefixCommon(ref (sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if ((Command_FocusVehicleMap.FocusedVehicle is { } vehicle ||
         UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out vehicle)))
    {
      __state = (Current.Game.currentMapIndex, new Command_FocusVehicleMap.FocusVehicle(vehicle));
      Current.Game.currentMapIndex = (sbyte)vehicle.CurrentLevel.Index;
    }
  }

  //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
  public static void Prefix(ref (sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (Event.current.type != EventType.Repaint || Find.MainTabsRoot.OpenTab != null)
    {
      return;
    }

    PrefixCommon(ref __state);
  }

  //FocusedVehicleをもとに戻しておく
  public static void Finalizer((sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (__state is null) return;

    Current.Game.currentMapIndex = __state.Value.Item1;
    __state.Value.Item2.Dispose();
  }
}

//Alt押した時のセル情報表示。MouseoverReadoutOnGUIと全く同じ
[HarmonyPatch(typeof(CellInspectorDrawer), "DrawMapInspector")]
[PatchLevel(Level.Safe)]
public static class Patch_CellInspectorDrawer_DrawMapInspector
{
  //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
  public static void Prefix(ref (sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }

  //FocusedVehicleをもとに戻しておく
  public static void Finalizer((sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (__state is null) return;

    Current.Game.currentMapIndex = __state.Value.Item1;
    __state.Value.Item2.Dispose();
  }
}

[HarmonyPatch(typeof(CellInspectorDrawer), nameof(CellInspectorDrawer.Update))]
[PatchLevel(Level.Safe)]
public static class Patch_CellInspectorDrawer_Update
{
  //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
  public static void Prefix(ref (sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (!KeyBindingDefOf.ShowCellInspector.IsDown) return;
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }

  //FocusedVehicleをもとに戻しておく
  public static void Finalizer((sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (__state is null) return;

    Current.Game.currentMapIndex = __state.Value.Item1;
    __state.Value.Item2.Dispose();
  }
}

//Alt押した時のセルの美しさ
[HarmonyPatch(typeof(BeautyDrawer), "DrawBeautyAroundMouse")]
public static class Patch_BeautyDrawer_DrawBeautyAroundMouse
{
  //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
  [PatchLevel(Level.Safe)]
  public static void Prefix(ref (sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }

  //FocusedVehicleがあればそのマップをFind.CurrentMapの代わりに使う
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

  //FocusedVehicleをもとに戻しておく
  [PatchLevel(Level.Safe)]
  public static void Finalizer((sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (__state is null) return;

    Current.Game.currentMapIndex = __state.Value.Item1;
    __state.Value.Item2.Dispose();
  }
}

//右下の温度表示
[HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
[PatchLevel(Level.Safe)]
public static class Patch_GlobalControls_TemperatureString
{
  //車両マップにマウスオーバーしていたらFocusedVehicleに入れておく。これでMouseCellが勝手にオフセットされる
  public static void Prefix(ref (sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    Patch_MouseoverReadout_MouseoverReadoutOnGUI.PrefixCommon(ref __state);
  }

  //FocusedVehicleをもとに戻しておく
  public static void Finalizer((sbyte, Command_FocusVehicleMap.FocusVehicle)? __state)
  {
    if (__state is null) return;

    Current.Game.currentMapIndex = __state.Value.Item1;
    __state.Value.Item2.Dispose();
  }
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

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = new CodeMatcher(instructions, generator);
    codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Quaternion_identity));
    codes.InsertAndAdvance(CachedMethodInfo.m_ToBaseMapCoord1.CallInstruction);
    codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
    codes.CreateLabelWithOffsets(1, out var label);
    codes.InsertAfter(
      new CodeInstruction(OpCodes.Ldloca_S, vehicle),
      CachedMethodInfo.m_FocusedOnVehicleMap.CallInstruction,
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      CachedMethodInfo.m_FullAngleQuat.CallInstruction,
      CachedMethodInfo.o_Quaternion_Multiply.CallInstruction);
    return codes.Instructions();
  }
}

//v, v2にToBaseMapCoordをしてDrawBoxRotatedにFocusedVehicle.FullRotation.AsAngleを渡す
//Widgets.DrawNumberOnMap(screenPos, intVec.x, Color.white) ->
//Widgets.DrawNumberOnMap(ConvertToVehicleMap(screenPos), intVec.x, Color.white)を3回
[HarmonyPatch(typeof(DesignationDragger), nameof(DesignationDragger.DraggerOnGUI))]
[PatchLevel(Level.Sensitive)]
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