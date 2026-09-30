using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.ProcessInputEvents))]
[PatchLevel(Level.Safe)]
public static class Patch_DesignatorManager_ProcessInputEvents
{
  internal static void Prefix([MustDisposeResource] ref FocusMapScope __state)
  {
    if (Command_FocusVehicleMap.FocusedVehicle is { } focused)
      __state = FocusMapScope.FocusMapUnsafe(focused.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatch(typeof(Building_OrbitalTradeBeacon), "MakeMatchingStockpile")]
public static class Patch_Building_OrbitalTradeBeacon_MakeMatchingStockpile
{
  internal static void Prefix(Thing __instance, [MustDisposeResource] ref FocusMapScope __state)
  {
    if (__instance.IsOnVehicleMapOf(out var vehicle))
      __state = FocusMapScope.FocusMapUnsafe(vehicle.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatch("RimWorld.Building_SunLamp", "MakeMatchingGrowZone")]
public static class Patch_Building_SunLamp_MakeMatchingGrowZone
{
  internal static void Prefix(Thing __instance, [MustDisposeResource] ref FocusMapScope __state)
  {
    if (__instance.IsOnVehicleMapOf(out var vehicle))
      __state = FocusMapScope.FocusMapUnsafe(vehicle.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatch(typeof(DesignationDragger), nameof(DesignationDragger.DraggerUpdate))]
[PatchLevel(Level.Cautious)]
public static class Patch_DesignationDragger_DraggerUpdate
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_CurrentViewRect, CachedMethodInfo.m_CurrentVehicleMapViewRect);
  }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.DesignatorManagerUpdate))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Designator_SelectedUpdate
{
  internal static void Prefix([MustDisposeResource] ref FocusMapScope __state)
  {
    if (Command_FocusVehicleMap.FocusedVehicle is { } focused)
      __state = FocusMapScope.FocusMapUnsafe(focused.CurrentLevel);
  }
  
  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_SelectedUpdate = AccessTools.Method(typeof(Designator), nameof(Designator.SelectedUpdate));
    foreach (var instruction in instructions)
    {
      yield return instruction;
      if (instruction.Calls(m_SelectedUpdate))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CodeInstruction.LoadField(typeof(DesignatorManager), "selectedDesignator");
        yield return ((Delegate)SelectedUpdatePostfix).Method.CallInstruction;
      }
    }
  }

  public static void SelectedUpdatePostfix(Designator ___selectedDesignator)
  {
    if (Command_FocusVehicleMap.FocusLockedVehicle != null) return;

    Command_FocusVehicleMap.FocusedVehicle = null;
    var mousePos = UI.MouseMapPosition();
    var flag = VehicleMapFlag.None;
    if (___selectedDesignator is Designator_Build { PlacingDef: ThingDef thingDef })
    {
      if (thingDef is VehicleBuildDef { thingToSpawn.thingClass: { } type } &&
          type.SameOrSubclassOf(typeof(VehiclePawnWithMap)))
        return;
      if (thingDef.HasComp<CompMapExpander>())
        flag |= VehicleMapFlag.ExpandableCells;
    }

    if (mousePos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, flag))
    {
      Command_FocusVehicleMap.FocusedVehicle = vehicle;
    }

    if (___selectedDesignator is Designator_AreaAllowed)
    {
      var selArea = Designator_AreaAllowed.selectedArea;
      if (selArea != null && selArea.Map != ___selectedDesignator.Map)
      {
        Designator_AreaAllowed.selectedArea = ___selectedDesignator.Map.areaManager.AllAreas
          .FirstOrDefault(a => a.AssignableAsAllowed() &&
                               a.InspectLabel == selArea.InspectLabel);
        if (Designator_AreaAllowed.selectedArea is null)
        {
          Messages.Message("VMF_AreaDeselect".Translate(selArea.InspectLabel), MessageTypeDefOf.RejectInput, false);
          Find.DesignatorManager.Deselect();
        }
      }
    }
  }
}

[HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
[PatchLevel(Level.Safe)]
public static class Patch_DesignatorManager_Deselect
{
  public static void Postfix()
  {
    if (Command_FocusVehicleMap.FocusLockedVehicle == null)
    {
      Command_FocusVehicleMap.FocusedVehicle = null;
    }
  }
}

[HarmonyPatch(typeof(Designator), nameof(Designator.CreateReverseDesignationGizmo))]
[PatchLevel(Level.Safe)]
public static class Patch_Designator_CreateReverseDesignationGizmo
{
  internal static void Prefix(Thing t, [MustDisposeResource] ref FocusMapScope __state)
  {
    if (t.IsOnVehicleMapOf(out var vehicle))
      __state = FocusMapScope.FocusMapUnsafe(vehicle.CurrentLevel);
  }

  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Designator_CreateReverseDesignationGizmo_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(typeof(Designator),
      t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<CreateReverseDesignationGizmo>")));
  }

  internal static void Prefix(Thing ___t, [MustDisposeResource] ref FocusMapScope __state)
  {
    if (___t.IsOnVehicleMapOf(out var vehicle))
      __state = FocusMapScope.FocusMapUnsafe(vehicle.CurrentLevel);
  }

  internal static void Finalizer(FocusMapScope __state) => __state.Dispose();
}

[HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.DrawDesignations))]
[PatchLevel(Level.Sensitive)]
public static class Patch_DesignationManager_DrawDesignations
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_CurrentViewRect, CachedMethodInfo.m_CurrentVehicleMapViewRect);
  }
}

[HarmonyPatch(typeof(GenGrid), nameof(GenGrid.InNoZoneEdgeArea))]
[PatchLevel(Level.Safe)]
public static class Patch_GenGrid_InNoZoneEdgeArea
{
  public static void Postfix(ref bool __result, Map map)
  {
    __result &= !map.IsVehicleMap;
  }
}

//利用可能なthingに車上マップ上のthingを含める
[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.ProcessInput))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Designator_Build_ProcessInput
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var code = instructions.ToList();
    var g_Count = AccessTools.PropertyGetter(typeof(List<Thing>), nameof(List<>.Count));
    var pos = code.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(g_Count));
    code.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Designator_Map),
      CodeInstruction.LoadLocal(4),
      ((Delegate)Patch_ItemAvailability_ThingsAvailableAnywhere.AddThingList).Method.CallInstruction
    ]);
    return code;
  }
}

[HarmonyPatch(typeof(Area), nameof(Area.MarkForDraw))]
[PatchLevel(Level.Cautious)]
public static class Patch_Area_MarkForDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap,
      CachedMethodInfo.g_VehicleMapUtility_CurrentMap);
  }
}

//CurrentMapがVehicleMapだったらマップエッジを描くことなんてないよ
[HarmonyPatch(typeof(GenDraw), "DrawMapEdgeLines")]
[PatchLevel(Level.Safe)]
public static class Patch_GenDraw_DrawMapEdgeLines
{
  public static bool Prefix() => !Find.CurrentMap.IsVehicleMap;
}