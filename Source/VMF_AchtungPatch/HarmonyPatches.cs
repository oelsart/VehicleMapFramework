using System;
using System.Collections.Generic;
using System.Reflection;
using AchtungMod;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using VehicleMapFramework;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VMF_AchtungPatch;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Achtung
{
  static Patches_Achtung()
  {
    VMF_Harmony.PatchCategory(PatchCategories.Achtung);
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch(typeof(Colonist), nameof(Colonist.UpdateOrderPos), typeof(Vector3), typeof(Predicate<IntVec3>))]
[PatchLevel(Level.Safe)]
public static class Patch_Colonist_UpdateOrderPos
{
  public static readonly Dictionary<IntVec3, Map> tmpDestMaps = [];

  private static int lastCachedTick;

  public static bool Prefix(Colonist __instance, ref Vector3 pos, Predicate<IntVec3> cellValidator, ref IntVec3 __result)
  {
    __instance.pawn.TargetMap = __instance.pawn.Map;
    if (Find.TickManager.TicksGame != lastCachedTick)
    {
      tmpDestMaps.Clear();
      lastCachedTick = Find.TickManager.TicksGame;
    }
    if (pos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None) || __instance.pawn.MapHeld.IsNonFocusedVehicleMapOf(out _))
    {
      __result = __instance.UpdateOrderPos(pos, cellValidator, vehicle);
      return false;
    }
    return true;
  }

  public static IntVec3 UpdateOrderPos(this Colonist colonist, Vector3 pos, Predicate<IntVec3> cellValidator, VehiclePawnWithMap vehicle)
  {
    IntVec3 destCell;
    IntVec3 destCellOnBaseMap;
    Map destMap;
    if (vehicle != null)
    {
      destCellOnBaseMap = pos.ToIntVec3();
      destCell = pos.ToVehicleMapCoord(vehicle).ToIntVec3();
      destMap = vehicle.VehicleMap;
    }
    else
    {
      destCell = destCellOnBaseMap = pos.ToIntVec3();
      destMap = colonist.pawn.MapHeldBaseMap();
    }
    colonist.pawn.TargetMap = destMap;

    if (AchtungLoader.IsSameSpotInstalled)
    {
      if (destCell.Standable(destMap) && (cellValidator?.Invoke(destCell) ?? true) && colonist.pawn.CanReach(destCell,
            PathEndMode.OnCell,
            Danger.Deadly,
            false,
            false,
            TraverseMode.ByPawn,
            destMap))
      {
        colonist.designation = destCell;
        tmpDestMaps[destCell] = destMap;
        return destCell;
      }
    }
    
    if (TryGetStandableMoveAnchor(destCell, destMap, out var moveAnchor))
      destCell = moveAnchor;

    var bestCell = IntVec3.Invalid;
    if (ModsConfig.BiotechActive && colonist.pawn.IsColonyMech && !MechanitorUtility.InMechanitorCommandRange(colonist.pawn, destCellOnBaseMap))
    {
      var overseer = colonist.pawn.GetOverseer();
      var map = overseer.MapHeld;
      if (map.BaseMapOrCaravan == colonist.pawn.MapHeldBaseMapOrCaravan)
      {
        var mechanitor = overseer.mechanitor;
        foreach (var newPos in GenRadial.RadialCellsAround(destCell, 20f, false))
        {
          if (mechanitor.CanCommandTo(newPos))
            if (destMap.pawnDestinationReservationManager.CanReserve(newPos, colonist.pawn, true)
                && newPos.Standable(destMap)
                && colonist.pawn.CanReach(newPos, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap)
               )
            {
              bestCell = newPos;
              tmpDestMaps[bestCell] = destMap;
              break;
            }
        }
      }
    }
    else
    {
      bestCell = CrossMapRCellFinder.BestOrderedGotoDestNear(destCell, colonist.pawn, null, true, destMap);
    }
    if (bestCell.InBounds(destMap))
    {
      colonist.designation = bestCell;
      tmpDestMaps[bestCell] = destMap;
      return bestCell;
    }
    return IntVec3.Invalid;
    
    static bool TryGetStandableMoveAnchor(IntVec3 cell, Map map, out IntVec3 result)
    {
      result = IntVec3.Invalid;
      if (map == null || !cell.IsValid || !cell.InBounds(map))
        return false;

      result = cell.Standable(map) ? cell : CellFinder.StandableCellNear(cell, map, 2.9f);
      return result.IsValid;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch("AchtungMod.Tools", "OrderTo")]
[PatchLevel(Level.Safe)]
public static class Patch_Tools_OrderTo
{
  public static bool Prefix(Pawn pawn, int x, int z)
  {
    var cell = new IntVec3(x, 0, z);
    if (pawn.TryGetTargetMap(out var map) && pawn.MapHeld != map && pawn.CanReach(cell,
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
      OrderTo(pawn, cell, map, exitSpot, enterSpot, spotsQueue);
      return false;
    }
    return true;
  }

  public static void OrderTo(Pawn pawn, IntVec3 cell, Map map, TargetInfo exitSpot, TargetInfo enterSpot, List<TraverseSpots> spotsQueue)
  {
    var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, cell).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, spotsQueue);
    job.playerForced = true;
    job.collideWithPawns = false;
    var baseMap = pawn.BaseMap();
    if (map == baseMap && baseMap.exitMapGrid.IsExitCell(cell))
      job.exitMapOnArrival = true;

    if (pawn.jobs?.IsCurrentJobPlayerInterruptible() ?? false)
      _ = pawn.jobs.TryTakeOrderedJob(job);
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch("AchtungMod.Tools", "LabelDrawPosFor")]
[PatchLevel(Level.Cautious)]
public static class Patch_Tools_LabelDrawPosFor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_IntVec3_ToVector3Shifted, ((Delegate)ToVector3ShiftedOffset).Method);
  }

  public static Vector3 ToVector3ShiftedOffset(ref IntVec3 cell)
  {
    var vector = cell.ToVector3Shifted();
    if (Patch_Colonist_UpdateOrderPos.tmpDestMaps.TryGetValue(cell, out var map) && map.IsNonFocusedVehicleMapOf(out var vehicle))
    {
      return vector.ToBaseMapCoord(vehicle);
    }
    return vector;
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Controller_HandleDrawing
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(Controller),
      t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<HandleDrawing>")));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      CachedMethodInfo.m_IntVec3_ToVector3Shifted,
      ((Delegate)Patch_Tools_LabelDrawPosFor.ToVector3ShiftedOffset).Method);
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch(typeof(Controller), nameof(Controller.MouseDown))]
public static class Patch_Controller_MouseDown
{
  private static VehiclePawnWithMap tmpFocusedMap;

  [PatchLevel(Level.Safe)]
  public static void Prefix(Vector3 pos)
  {
    tmpFocusedMap = Command_FocusVehicleMap.FocusedVehicle;
    if (pos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None))
    {
      Command_FocusVehicleMap.FocusedVehicle = vehicle;
      CrossMapReachabilityUtility.DestMapGlobal = vehicle.CurrentLevel;
      GenUIOnVehicle.vehicleForSelector = vehicle;
    }
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_FromVector3 = ((Func<Vector3, IntVec3>)IntVec3.FromVector3).Method;
    var m_FromVector3Offset = ((Delegate)FromVector3Offset).Method;
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap),
      (m_FromVector3, m_FromVector3Offset));
  }

  private static IntVec3 FromVector3Offset(Vector3 pos)
  {
    return IntVec3.FromVector3(pos.ToVehicleMapCoord());
  }

  [PatchLevel(Level.Safe)]
  public static void Finalizer()
  {
    Command_FocusVehicleMap.FocusedVehicle = tmpFocusedMap;
    CrossMapReachabilityUtility.DestMapGlobal = null;
    GenUIOnVehicle.vehicleForSelector = null;
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch(typeof(Controller), "TryGetPlainDraftedMoveAnchor")]
[PatchLevel(Level.Cautious)]
public static class Patch_Tools_TryGetPlainDraftedMoveAnchor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_Controller_MouseDown.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.Achtung)]
[HarmonyPatch(typeof(Controller), "PawnsUnderMouse")]
public static class Patch_Tools_PawnsUnderMouse
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(ref Vector3 pos) => pos = pos.ToVehicleMapCoord();
  
  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      ((Delegate)GenUI.ThingsUnderMouse).Method,
      ((Func<Vector3, float, TargetingParameters, ITargetingSource, List<Thing>>)GenUIOnVehicle.ThingsUnderMouse).Method
      );
  }
}