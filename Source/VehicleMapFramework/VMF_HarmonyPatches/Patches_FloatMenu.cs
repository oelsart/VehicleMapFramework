using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuMakerMap_GetOptions
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(GenGrid),
        nameof(GenGrid.InBounds),
        [typeof(Vector3), typeof(Map)])))
      .SetInstruction(CodeInstruction.Call(typeof(Patch_FloatMenuMakerMap_GetOptions), nameof(InBounds)))
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(GenGrid),
        nameof(GenGrid.InBounds),
        [typeof(IntVec3), typeof(Map)])))
      .Insert(
        new CodeInstruction(OpCodes.Pop),
        CodeInstruction.LoadArgument(2),
        new CodeInstruction(OpCodes.Ldind_Ref),
        CodeInstruction.LoadField(typeof(FloatMenuContext), nameof(FloatMenuContext.map)))
      .InstructionEnumeration();
  }

  private static bool InBounds(Vector3 clickPos, Map map)
  {
    return map.IsVehicleMap ? clickPos.TryGetVehicleMap(map, out _, VehicleMapFlag.None) : clickPos.InBounds(map);
  }
}

[HarmonyPatch(typeof(FloatMenuContext), MethodType.Constructor, typeof(List<Pawn>), typeof(Vector3), typeof(Map))]
public static class Patch_FloatMenuContext_Constructor
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(List<Pawn> selectedPawns, ref Vector3 clickPosition, ref Map map)
  {
    if (selectedPawns.All(p => p is VehiclePawnWithMap)) return;
    if (!clickPosition.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None)) return;

    GenUIOnVehicle.vehicleForSelector = vehicle;
    clickPosition = clickPosition.ToVehicleMapCoord(vehicle);
    map = vehicle.CurrentLevel;
  }

  [PatchLevel(Level.Safe)]
  public static void Finalizer(FloatMenuContext __instance)
  {
    Pawn pawn;
    if (!__instance.IsMultiselect && (pawn = __instance.FirstSelectedPawn) != null)
      pawn.TargetInfo = new TargetInfo(__instance.ClickedCell, __instance.map);
    GenUIOnVehicle.vehicleForSelector = null;
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_ThingsUnderMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.ThingsUnderMouse));
    var m_ThingsUnderMouseOnVehicle = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.ThingsUnderMouse), [typeof(Vector3), typeof(float), typeof(TargetingParameters), typeof(ITargetingSource)]);
    return instructions.MethodReplacer(m_ThingsUnderMouse, m_ThingsUnderMouseOnVehicle);
  }
}

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ShouldGenerateFloatMenuForPawn))]
[PatchLevel(Level.Cautious)]
public static class Patch_FloatMenuMakerMap_ShouldGenerateFloatMenuForPawn
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .InsertAndAdvance(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
      .InsertAfter(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(FloatMenuOptionProvider_ExtinguishFires), "GetSingleOption")]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(FloatMenuContext),
        nameof(FloatMenuContext.FirstSelectedPawn))))
      .RemoveInstruction()
      .SetInstruction(CodeInstruction.LoadField(typeof(FloatMenuContext), nameof(FloatMenuContext.map)))
      .MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(FloatMenuContext),
        nameof(FloatMenuContext.ClickedCell))))
      .DeclareLocal(typeof(FloatMenuContext), out var context)
      .DeclareLocal(typeof(Pawn), out var pawn)
      .DeclareLocal(typeof(VirtualTeleporter?), out var teleporter)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Stloc_S, context),
        new CodeInstruction(OpCodes.Dup),
        new CodeInstruction(OpCodes.Dup),
        new CodeInstruction(OpCodes.Stloc_S, pawn),
        new CodeInstruction(OpCodes.Ldloc_S, context),
        CodeInstruction.Call(typeof(Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption),
          nameof(Teleport)),
        new CodeInstruction(OpCodes.Stloc_S, teleporter),
        new CodeInstruction(OpCodes.Ldloc_S, context))
      .MatchStartForward(new CodeMatch(OpCodes.Call))
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldloc_S, pawn),
        new CodeInstruction(OpCodes.Ldloc_S, teleporter),
        CodeInstruction.Call(typeof(Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption),
          nameof(Dispose)))
      .InstructionEnumeration();
  }

  internal static VirtualTeleporter? Teleport(Pawn pawn, FloatMenuContext context)
  {
    return pawn.Map != context.map ? new VirtualTeleporter(pawn, context.map) : null;
  }

  internal static void Dispose(Pawn pawn, VirtualTeleporter? __state)
  {
    pawn?.RemoveDepartMap();
    __state?.Dispose();
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(typeof(FloatMenuOptionProvider_ExtinguishFires), t => { return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<GetSingleOption>")); });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(FloatMenuContext),
        nameof(FloatMenuContext.ClickedCell))))
      .DeclareLocal(typeof(FloatMenuContext), out var context)
      .DeclareLocal(typeof(Pawn), out var pawn)
      .DeclareLocal(typeof(VirtualTeleporter?), out var teleporter)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Stloc_S, context),
        new CodeInstruction(OpCodes.Dup),
        new CodeInstruction(OpCodes.Dup),
        new CodeInstruction(OpCodes.Stloc_S, pawn),
        new CodeInstruction(OpCodes.Ldloc_S, context),
        CodeInstruction.Call(typeof(Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption),
          nameof(Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption.Teleport)),
        new CodeInstruction(OpCodes.Stloc_S, teleporter),
        new CodeInstruction(OpCodes.Ldloc_S, context))
      .MatchStartForward(new CodeMatch(OpCodes.Call))
      .InsertAfterAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_S, pawn),
        new CodeInstruction(OpCodes.Ldloc_S, teleporter),
        CodeInstruction.Call(typeof(Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption),
          nameof(Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption.Dispose)))
      .MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(FloatMenuContext),
        nameof(FloatMenuContext.FirstSelectedPawn))))
      .RemoveInstruction()
      .SetInstruction(CodeInstruction.LoadField(typeof(FloatMenuContext), nameof(FloatMenuContext.map)))
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(GenUI), nameof(GenUI.TargetsAt))]
[PatchLevel(Level.Safe)]
public static class Patch_GenUI_TargetsAt
{
  public static bool Prefix(Vector3 clickPos, TargetingParameters clickParams, bool thingsOnly, ITargetingSource source, ref IEnumerable<LocalTargetInfo> __result)
  {
    bool convToVehicleMap;
    if (!(convToVehicleMap = Find.CurrentMap.IsVehicleMapOf(out var vehicle)))
    {
      clickPos.TryGetVehicleMap(Find.CurrentMap, out vehicle, VehicleMapFlag.None);
    }
    if (vehicle != null)
    {
      __result = GenUIOnVehicle.TargetsAt(clickPos, clickParams, thingsOnly, source, vehicle, convToVehicleMap);
      return false;
    }
    return true;
  }
}

[HarmonyPatch(typeof(FloatMenuMap), "StillValid")]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuMap_StillValid
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadField(typeof(FloatMenuOption), nameof(FloatMenuOption.revalidateClickTarget)),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingBaseMapCoord)
    ]);
    return codes;
  }
}

//ベースマップに居る時のFloatMenuにもHoldingPlatform検索を足しときます
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_Entity_GetOptionFor
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.FindIncludingInnerTypes(typeof(FloatMenuOptionProvider_CaptureEntity), GetOptionsFor_MoveNext);
    yield return AccessTools.FindIncludingInnerTypes(typeof(FloatMenuOptionProvider_TransferEntity), GetOptionsFor_MoveNext);
    yield break;

    static MethodBase GetOptionsFor_MoveNext(Type t)
    {
      return !t.Name.Contains("<GetOptionsFor>") ? null : AccessTools.Method(t, "MoveNext");
    }
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    var m_AllBuildingsColonistOfClass = AccessTools.Method(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass)).MakeGenericMethod(typeof(Building_HoldingPlatform));
    codes.MatchStartForward(CodeMatch.Calls(m_AllBuildingsColonistOfClass)).Advance();
    codes.Insert(CodeInstruction.Call(typeof(Patch_FloatMenuOptionProvider_Entity_GetOptionFor), nameof(AddHoldingPlatforms)));
    codes.MatchStartBackwards(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
    codes.Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing);

    var m_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
    var m_ClosestThing_Global_ReachableCrossMap = AccessTools.Method(typeof(GenClosestCrossMap),
      nameof(GenClosestCrossMap.ClosestThing_Global_Reachable),
      [
        typeof(IntVec3),
        typeof(Map),
        typeof(IEnumerable<Thing>),
        typeof(PathEndMode),
        typeof(TraverseParms),
        typeof(float),
        typeof(Predicate<Thing>),
        typeof(Func<Thing, float>),
        typeof(bool)
      ]);
    codes.MatchStartForward(CodeMatch.Calls(m_ClosestThing_Global_Reachable));
    codes.Operand = m_ClosestThing_Global_ReachableCrossMap;
    return codes.Instructions();
  }

  private static IEnumerable<Building_HoldingPlatform> AddHoldingPlatforms(IEnumerable<Building_HoldingPlatform> enumerable)
  {
    return enumerable.Concat(VehiclePawnWithMapCache.AllVehiclesOn(Find.CurrentMap).SelectMany(v => v.VehicleMap.listerBuildings.AllBuildingsColonistOfClass<Building_HoldingPlatform>()));
  }
}

[HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.StartInteraction))]
[PatchLevel(Level.Safe)]
public static class Patch_MultiPawnGotoController_StartInteraction
{
  public static void Prefix(ref IntVec3 mouseCell)
  {
    if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None))
    {
      mouseCell = mouseCell.ToBaseMapCoord(vehicle);
    }
  }
}

//複数ポーンを選択してる時の行き先計算
[HarmonyPatch(typeof(MultiPawnGotoController), "RecomputeDestinations")]
public static class Patch_MultiPawnGotoController_RecomputeDestinations
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(List<Pawn> ___pawns)
  {
    ___pawns.Do(p => p.RemoveTargetInfo());
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.ProcessInputEvents))]
[PatchLevel(Level.Cautious)]
public static class Patch_MultiPawnGotoController_ProcessInputEvents
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.Draw))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MultiPawnGotoController_Draw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_ToVector3ShiftedWithAltitude = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(float)]);
    var m_ToVector3ShiftedOffsetWithAltitude = AccessTools.Method(typeof(Patch_MultiPawnGotoController_Draw), nameof(ToVector3ShiftedOffsetWithAltitude));
    var m_Fogged = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), [typeof(IntVec3), typeof(Map)]);
    var m_FoggedOffset = AccessTools.Method(typeof(Patch_MultiPawnGotoController_Draw), nameof(FoggedOffset));
    var num = 0;
    foreach (var instruction in instructions)
    {
      if (num < 2 && instruction.opcode == OpCodes.Call && instruction.OperandIs(m_ToVector3ShiftedWithAltitude))
      {
        yield return CodeInstruction.LoadLocal(5);
        instruction.operand = m_ToVector3ShiftedOffsetWithAltitude;
        num++;
      }
      if (instruction.opcode == OpCodes.Call && instruction.OperandIs(m_Fogged))
      {
        yield return new CodeInstruction(OpCodes.Pop);
        yield return CodeInstruction.LoadLocal(5);
        instruction.operand = m_FoggedOffset;
      }
      yield return instruction;
    }
  }

  private static Vector3 ToVector3ShiftedOffsetWithAltitude(ref IntVec3 intVec, float AddedAltitude, Pawn pawn)
  {
    return pawn.TryGetTargetMap(out var map) ? intVec.ToVector3Shifted().ToBaseMapCoord(map).WithY(AddedAltitude) : intVec.ToVector3ShiftedWithAltitude(AddedAltitude);
  }

  private static bool FoggedOffset(IntVec3 intVec, Pawn pawn)
  {
    return pawn.TryGetTargetMap(out var map) ? intVec.ToBaseMapCoord(map).Fogged(map.BaseMap()) : intVec.Fogged(pawn.Map);
  }
}

[HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.OnGUI))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MultiPawnGotoController_OnGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_ToUIRect = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToUIRect));
    var m_ToUIRectOffset = AccessTools.Method(typeof(Patch_MultiPawnGotoController_OnGUI), nameof(ToUIRectOffset));
    var m_Fogged = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), [typeof(IntVec3), typeof(Map)]);
    var m_FoggedOffset = AccessTools.Method(typeof(Patch_MultiPawnGotoController_Draw), "FoggedOffset");
    foreach (var instruction in instructions)
    {
      if (instruction.opcode == OpCodes.Call && instruction.OperandIs(m_ToUIRect))
      {
        yield return CodeInstruction.LoadLocal(1);
        instruction.operand = m_ToUIRectOffset;
      }
      if (instruction.opcode == OpCodes.Call && instruction.OperandIs(m_Fogged))
      {
        yield return new CodeInstruction(OpCodes.Pop);
        yield return CodeInstruction.LoadLocal(1);
        instruction.operand = m_FoggedOffset;
      }
      yield return instruction;
    }
  }

  private static Rect ToUIRectOffset(ref IntVec3 intVec, Pawn pawn)
  {
    var mapPos = ToVector3Offset(intVec, pawn);
    var vector = mapPos.MapToUIPosition();
    var vector2 = (mapPos + new Vector3(1f, 0f, 1f)).MapToUIPosition();
    return new Rect(vector.x, vector2.y, vector2.x - vector.x, vector.y - vector2.y);
  }

  private static Vector3 ToVector3Offset(IntVec3 intVec, Pawn pawn)
  {
    if (pawn.TryGetTargetMap(out var map))
    {
      if (map.IsNonFocusedVehicleMapOf(out var vehicle))
      {
        return Ext_Math.RotatePoint(intVec.ToVector3(), intVec.ToVector3Shifted(), vehicle.FullRotation.AsAngle).ToBaseMapCoord(vehicle);
      }
    }
    return intVec.ToVector3();
  }
}

//行き先がVehicleMap上にあると登録されているかsearcherがVehicleMap上に居る時はBestOrderedGotoDestNearを置き換え
//ジャンプ時のTargetVehicleも考慮にいれるよう変更
[HarmonyPatch(typeof(RCellFinder), nameof(RCellFinder.BestOrderedGotoDestNear))]
[PatchLevel(Level.Safe)]
public static class Patch_RCellFinder_BestOrderedGotoDestNear
{
  public static bool Prefix(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, bool reachable, ref IntVec3 __result)
  {
    VehiclePawnWithMap vehicle = null;
    if (searcher.TryGetTargetMap(out var map))
    {
      __result = CrossMapRCellFinder.BestOrderedGotoDestNear(root, searcher, cellValidator, reachable, map);
      if (__result.IsValid)
      {
        searcher.TargetInfo = new TargetInfo(__result, map);
        return false;
      }
    }
    else if (searcher.IsOnNonFocusedVehicleMapOf(out var vehicle2) || root.InBounds(Find.CurrentMap) && root.TryGetVehicleMap(Find.CurrentMap, out vehicle))
    {
      vehicle ??= vehicle2;
      if (vehicle is null && vehicle2 is not { Spawned: true })
        UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out vehicle, VehicleMapFlag.None);
      var dest = vehicle != null ? root.ToVehicleMapCoord(vehicle) : root;
      map = vehicle != null ? vehicle.CurrentLevel : Find.CurrentMap;
      __result = CrossMapRCellFinder.BestOrderedGotoDestNear(
        dest,
        searcher,
        cellValidator,
        reachable,
        map);
      if (__result.IsValid)
      {
        searcher.TargetInfo = new TargetInfo(__result, map);
        return false;
      }
    }
    return true;
  }
}

[HarmonyPatch(typeof(RCellFinder), nameof(RCellFinder.TryFindGoodAdjacentSpotToTouch))]
[PatchLevel(Level.Safe)]
public static class Patch_RCellFinder_TryFindGoodAdjacentSpotToTouch
{
  public static bool Prefix(Pawn toucher, Thing touchee, ref IntVec3 result, ref bool __result)
  {
    var thingMap = touchee.MapHeld;
    if (thingMap != null && toucher.Map != thingMap && thingMap.BaseMapOrCaravan == toucher.BaseMapOrCaravan)
    {
      __result = CrossMapRCellFinder.TryFindGoodAdjacentSpotToTouch(toucher, touchee, out result);
      return false;
    }
    return true;
  }
}

[HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnGotoAction))]
[PatchLevel(Level.Safe)]
public static class Patch_FloatMenuOptionProvider_DraftedMove_PawnGotoAction
{
  public static bool Prefix(IntVec3 clickCell, Pawn pawn, IntVec3 gotoLoc)
  {
    if (pawn.TryGetTargetMap(out var map) && pawn.Map != map)
    {
      //BestOrderedGotoDestNearが通ってるはずなのでキャッシュからexitSpotとenterSpotを取ってくるだけの最終確認CanReach
      if (pawn.CanReach(gotoLoc,
            PathEndMode.OnCell,
            Danger.Deadly,
            false,
            false,
            TraverseMode.ByPawn,
            map,
            out var exitSpot,
            out var enterSpot,
            out var spotsQueue))
      {
        PawnGotoAction(clickCell, pawn, map, exitSpot, enterSpot, spotsQueue, gotoLoc);
      }
      return false;
    }
    return true;
  }

  public static void PawnGotoAction(IntVec3 clickCell, Pawn pawn, Map map, TargetInfo exitSpot, TargetInfo enterSpot,
    List<TraverseSpots> spotsQueue, LocalTargetInfo dest)
  {
    bool flag;
    var baseMap = map.BaseMap();
    if (pawn.Map == map && pawn.Position == dest.Cell)
    {
      flag = true;
      if (pawn.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps)
      {
        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
      }
    }
    else if (pawn.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps && pawn.Map == map && pawn.CurJob.targetA == dest)
    {
      flag = true;
    }
    else
    {
      var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, dest).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, spotsQueue);
      if (!map.IsVehicleMapOf(out _) && map.exitMapGrid.IsExitCell(clickCell))
      {
        job.exitMapOnArrival = !pawn.IsColonyMech;
      }
      else if (!baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid && pawn.Map == baseMap && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3) && baseMap.Parent.GetComponent<FormCaravanComp>() != null &&
               MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + baseMap.uniqueID, 60f))
      {
        Messages.Message(
          baseMap.Parent.GetComponent<FormCaravanComp>().CanFormOrReformCaravanNow
            ? "MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate()
            : "MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(),
          baseMap.Parent,
          MessageTypeDefOf.RejectInput,
          false);
      }
      flag = pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
    }
    if (flag)
    {
      FleckMaker.Static(dest.Cell, map, FleckDefOf.FeedbackGoto);
    }
  }
}

[HarmonyPatch(typeof(FloatMenuOptionProvider_WorkGivers), "GetWorkGiverOption")]
[PatchLevel(Level.Safe)]
public static class Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiverOption
{
  public static void Prefix(Pawn pawn, WorkGiverDef workGiver, LocalTargetInfo target, FloatMenuContext context, ref VirtualTeleporter? __state)
  {
    if (JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, context.map, workGiver))
    {
      return;
    }
    if (pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, context.map))
    {
      __state = new VirtualTeleporter(pawn, context.map, target.Cell);
    }
  }

  public static void Finalizer(Pawn pawn, WorkGiverDef workGiver, LocalTargetInfo target, FloatMenuContext context, VirtualTeleporter? __state, FloatMenuOption __result)
  {
    if (__state is null)
    {
      return;
    }
    __state.Value.Dispose();

    if ((!__result?.Disabled ?? false) && __result.action != null && JobAcrossMapsUtility.NeedWrapGotoDestMapJob(workGiver.Worker as WorkGiver_Scanner) &&
        pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, context.map, out var exitSpot, out var enterSpot, out var spotsQueue))
    {
      __result.action = (() => { JobAcrossMapsUtility.StartGotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue); }) + __result.action;
    }
  }
}
