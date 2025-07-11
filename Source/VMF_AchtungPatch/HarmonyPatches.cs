﻿using AchtungMod;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        VMF_Harmony.PatchCategory("VMF_Patches_Achtung");

        //var type = AccessTools.TypeByName("AchtungMod.FloatMenuMakerMap_AddJobGiverWorkOrders_Patch");
        //var prefix = AccessTools.Method(type, "Prefix");
        //var postfix = AccessTools.Method(type, "Postfix");
        //var transpiler = AccessTools.Method(type, "Transpiler");
        //var original = AccessTools.Method(typeof(FloatMenuMakerOnVehicle), "AddJobGiverWorkOrders");
        //VMF_Harmony.Instance.Patch(original, prefix, postfix, transpiler);

        //type = AccessTools.TypeByName("AchtungMod.FloatMenuMakerMap_ChoicesAtFor_Postfix");
        //postfix = AccessTools.Method(type, "Postfix");
        //original = AccessTools.Method(typeof(FloatMenuMakerOnVehicle), nameof(FloatMenuMakerOnVehicle.ChoicesAtFor));
        //VMF_Harmony.Instance.Patch(original, postfix: postfix);

        //type = AccessTools.TypeByName("AchtungMod.FloatMenuMakerMap_ScannerShouldSkip_Patch");
        //prefix = AccessTools.Method(type, "Prefix");
        //original = AccessTools.Method(typeof(FloatMenuMakerOnVehicle), "ScannerShouldSkip");
        //VMF_Harmony.Instance.Patch(original, prefix: prefix);
    }
}

[HarmonyPatchCategory("VMF_Patches_Achtung")]
[HarmonyPatch(typeof(Colonist), nameof(Colonist.UpdateOrderPos))]
public static class Patch_Colonist_UpdateOrderPos
{
    public static bool Prefix(Colonist __instance, ref Vector3 pos, ref IntVec3 __result)
    {
        TargetMapManager.SetTargetMap(__instance.pawn, __instance.pawn.Map);
        if (Find.TickManager.TicksGame != lastCachedTick)
        {
            tmpDestMaps.Clear();
            lastCachedTick = Find.TickManager.TicksGame;
        }
        if (pos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false) || __instance.pawn.MapHeld.IsNonFocusedVehicleMapOf(out _))
        {
            __result = UpdateOrderPos(__instance, pos, vehicle);
            return false;
        }
        return true;
    }

    public static IntVec3 UpdateOrderPos(this Colonist colonist, Vector3 pos, VehiclePawnWithMap vehicle)
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
        TargetMapManager.SetTargetMap(colonist.pawn, destMap);
        tmpExitSpot = TargetInfo.Invalid;
        tmpEnterSpot = TargetInfo.Invalid;

        if (AchtungLoader.IsSameSpotInstalled)
        {
            if (destCell.Standable(destMap) && colonist.pawn.CanReach(destCell, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap, out tmpExitSpot, out tmpEnterSpot))
            {
                colonist.designation = destCell;
                tmpDestMaps[destCell] = destMap;
                return destCell;
            }
        }

        var bestCell = IntVec3.Invalid;
        if (ModsConfig.BiotechActive && colonist.pawn.IsColonyMech && MechanitorUtility.InMechanitorCommandRange(colonist.pawn, destCellOnBaseMap) == false)
        {
            var overseer = colonist.pawn.GetOverseer();
            var map = overseer.MapHeld;
            if (map.BaseMap() == colonist.pawn.MapHeldBaseMap())
            {
                var mechanitor = overseer.mechanitor;
                foreach (var newPos in GenRadial.RadialCellsAround(destCell, 20f, false))
                    if (mechanitor.CanCommandTo(newPos))
                        if (destMap.pawnDestinationReservationManager.CanReserve(newPos, colonist.pawn, true)
                            && newPos.Standable(destMap)
                            && colonist.pawn.CanReach(newPos, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap, out tmpExitSpot, out tmpEnterSpot)
                        )
                        {
                            bestCell = newPos;
                            tmpDestMaps[newPos] = destMap;
                            break;
                        }
            }
        }
        else
            bestCell = CrossMapReachabilityUtility.BestOrderedGotoDestNear(destCell, colonist.pawn, null, destMap, out tmpExitSpot, out tmpEnterSpot);
        if (bestCell.InBounds(destMap))
        {
            colonist.designation = bestCell;
            tmpDestMaps[bestCell] = destMap;
            return bestCell;
        }
        return IntVec3.Invalid;
    }

    public static TargetInfo tmpExitSpot;

    public static TargetInfo tmpEnterSpot;

    public static Dictionary<IntVec3, Map> tmpDestMaps = new Dictionary<IntVec3, Map>();

    private static int lastCachedTick;
}

[HarmonyPatchCategory("VMF_Patches_Achtung")]
[HarmonyPatch("AchtungMod.Tools", "OrderTo")]
public static class Patch_Tools_OrderTo
{
    public static bool Prefix(Pawn pawn, int x, int z)
    {
        TargetMapManager.RemoveTargetInfo(pawn);
        if (Patch_Colonist_UpdateOrderPos.tmpDestMaps.TryGetValue(new IntVec3(x, 0, z), out var map) && map != null)
        {
            OrderTo(pawn, x, z);
            return false;
        }
        return true;
    }

    public static void OrderTo(Pawn pawn, int x, int z)
    {
        var bestCell = new IntVec3(x, 0, z);
        var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, bestCell).SetSpotsToJobAcrossMaps(pawn, Patch_Colonist_UpdateOrderPos.tmpExitSpot, Patch_Colonist_UpdateOrderPos.tmpEnterSpot);
        job.playerForced = true;
        job.collideWithPawns = false;
        var baseMap = pawn.BaseMap();
        if (Patch_Colonist_UpdateOrderPos.tmpDestMaps[bestCell] == baseMap && baseMap.exitMapGrid.IsExitCell(bestCell))
            job.exitMapOnArrival = true;

        if (pawn.jobs?.IsCurrentJobPlayerInterruptible() ?? false)
            _ = pawn.jobs.TryTakeOrderedJob(job);
    }
}

[HarmonyPatchCategory("VMF_Patches_Achtung")]
[HarmonyPatch("AchtungMod.Tools", "LabelDrawPosFor")]
public static class Patch_Tools_LabelDrawPosFor
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_IntVec3_ToVector3Shifted, m_ToVector3ShiftedOffset);
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

    public static MethodInfo m_ToVector3ShiftedOffset = AccessTools.Method(typeof(Patch_Tools_LabelDrawPosFor), nameof(ToVector3ShiftedOffset));
}

[HarmonyPatchCategory("VMF_Patches_Achtung")]
[HarmonyPatch]
public static class Patch_Controller_HandleDrawing
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(Controller), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<HandleDrawing>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_IntVec3_ToVector3Shifted, Patch_Tools_LabelDrawPosFor.m_ToVector3ShiftedOffset);
    }
}

[HarmonyPatchCategory("VMF_Patches_Achtung")]
[HarmonyPatch(typeof(Controller), nameof(Controller.MouseDown))]
public static class Patch_Controller_MouseDown
{
    public static void Prefix(Vector3 pos)
    {
        tmpFocusedMap = Command_FocusVehicleMap.FocusedVehicle;
        if (pos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
        {
            Command_FocusVehicleMap.FocusedVehicle = vehicle;
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_FromVector3 = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.FromVector3), [typeof(Vector3)]);
        var m_FromVector3Offset = AccessTools.Method(typeof(Patch_Controller_MouseDown), nameof(FromVector3Offset));
        return instructions.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap)
            .MethodReplacer(m_FromVector3, m_FromVector3Offset);
    }

    private static IntVec3 FromVector3Offset(Vector3 pos)
    {
        return IntVec3.FromVector3(pos.ToVehicleMapCoord());
    }

    public static void Finalizer()
    {
        Command_FocusVehicleMap.FocusedVehicle = tmpFocusedMap;
    }

    private static VehiclePawnWithMap tmpFocusedMap;
}
