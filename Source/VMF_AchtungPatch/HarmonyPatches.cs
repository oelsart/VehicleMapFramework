using AchtungMod;
using HarmonyLib;
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
        VMF_Harmony.PatchCategory(Category);
    }

    public const string Category = "VMF_Patches_Achtung";
}

[HarmonyPatchCategory(Patches_Achtung.Category)]
[HarmonyPatch(typeof(Colonist), nameof(Colonist.UpdateOrderPos))]
[PatchLevel(Level.Safe)]
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

        if (AchtungLoader.IsSameSpotInstalled)
        {
            if (destCell.Standable(destMap) && colonist.pawn.CanReach(destCell, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap, out _, out _))
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
                            && colonist.pawn.CanReach(newPos, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap, out _, out _)
                        )
                        {
                            bestCell = newPos;
                            tmpDestMaps[bestCell] = destMap;
                            break;
                        }
            }
        }
        else
            bestCell = CrossMapReachabilityUtility.BestOrderedGotoDestNear(destCell, colonist.pawn, null, destMap, out _, out _);
        if (bestCell.InBounds(destMap))
        {
            colonist.designation = bestCell;
            tmpDestMaps[bestCell] = destMap;
            return bestCell;
        }
        return IntVec3.Invalid;
    }

    public static Dictionary<IntVec3, Map> tmpDestMaps = [];

    private static int lastCachedTick;
}

[HarmonyPatchCategory(Patches_Achtung.Category)]
[HarmonyPatch("AchtungMod.Tools", "OrderTo")]
[PatchLevel(Level.Safe)]
public static class Patch_Tools_OrderTo
{
    public static bool Prefix(Pawn pawn, int x, int z)
    {
        var cell = new IntVec3(x, 0, z);
        if (TargetMapManager.HasTargetMap(pawn, out var map) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
        {
            OrderTo(pawn, cell, map, exitSpot, enterSpot);
            return false;
        }
        return true;
    }

    public static void OrderTo(Pawn pawn, IntVec3 cell, Map map, TargetInfo exitSpot, TargetInfo enterSpot)
    {
        var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, cell).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot);
        job.playerForced = true;
        job.collideWithPawns = false;
        var baseMap = pawn.BaseMap();
        if (map == baseMap && baseMap.exitMapGrid.IsExitCell(cell))
            job.exitMapOnArrival = true;

        if (pawn.jobs?.IsCurrentJobPlayerInterruptible() ?? false)
            _ = pawn.jobs.TryTakeOrderedJob(job);
    }
}

[HarmonyPatchCategory(Patches_Achtung.Category)]
[HarmonyPatch("AchtungMod.Tools", "LabelDrawPosFor")]
[PatchLevel(Level.Cautious)]
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

[HarmonyPatchCategory(Patches_Achtung.Category)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
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

[HarmonyPatchCategory(Patches_Achtung.Category)]
[HarmonyPatch(typeof(Controller), nameof(Controller.MouseDown))]
[PatchLevel(Level.Safe)]
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
