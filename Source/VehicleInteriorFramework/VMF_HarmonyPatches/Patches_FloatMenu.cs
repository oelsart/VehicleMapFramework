using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [HarmonyPatch(typeof(FloatMenuContext), MethodType.Constructor, typeof(List<Pawn>), typeof(Vector3), typeof(Map))]
    public static class Patch_FloatMenuContext_Constructor
    {
        public static void Prefix(ref Vector3 clickPosition, ref Map map)
        {
            if (clickPosition.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
            {
                GenUIOnVehicle.vehicleForSelector = vehicle;
                clickPosition = clickPosition.ToVehicleMapCoord(vehicle);
                map = vehicle.VehicleMap;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ThingsUnderMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.ThingsUnderMouse));
            var m_ThingsUnderMouseOnVehicle = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.ThingsUnderMouse), new[] { typeof(Vector3), typeof(float), typeof(TargetingParameters), typeof(ITargetingSource) });
            return instructions.MethodReplacer(m_ThingsUnderMouse, m_ThingsUnderMouseOnVehicle);
        }
    }

    [HarmonyPatch(typeof(GenUI), nameof(GenUI.TargetsAt))]
    public static class Patch_GenUI_TargetsAt
    {
        public static bool Prefix(Vector3 clickPos, TargetingParameters clickParams, bool thingsOnly, ITargetingSource source, ref IEnumerable<LocalTargetInfo> __result)
        {
            bool convToVehicleMap;
            if (!(convToVehicleMap = Find.CurrentMap.IsVehicleMapOf(out var vehicle)))
            {
                clickPos.TryGetVehicleMap(Find.CurrentMap, out vehicle, false);
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
    public static class Patch_FloatMenuMap_StillValid
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_1);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(FloatMenuOption), nameof(FloatMenuOption.revalidateClickTarget)),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_ToThingBaseMapCoord2)
            });
            return codes;
        }
    }

    //[HarmonyPatch]
    //public static class Patch_EnterPortalUtility_GetFloatMenuOptFor
    //{
    //    [HarmonyTranspiler]
    //    [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(Pawn), typeof(IntVec3))]
    //    public static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
    //    {
    //        return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
    //    }

    //    [HarmonyTranspiler]
    //    [HarmonyPatch(typeof(EnterPortalUtility), nameof(EnterPortalUtility.GetFloatMenuOptFor), typeof(List<Pawn>), typeof(IntVec3))]
    //    public static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
    //    {
    //        return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
    //    }
    //}

    //ベースマップに居る時のFloatMenuにもHoldingPlatform検索を足しときます
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    public static class Patch_FloatMenuMakerMap_AddHumanlikeOrders
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_AllBuildingsColonistOfClass = AccessTools.Method(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass)).MakeGenericMethod(typeof(Building_HoldingPlatform));
            var m_AddHoldingPlatforms = AccessTools.Method(typeof(Patch_FloatMenuMakerMap_AddHumanlikeOrders), nameof(Patch_FloatMenuMakerMap_AddHumanlikeOrders.AddHoldingPlatforms));
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Callvirt && instruction.OperandIs(m_AllBuildingsColonistOfClass))
                {
                    yield return new CodeInstruction(OpCodes.Call, m_AddHoldingPlatforms);
                }
            }
        }

        private static IEnumerable<Building_HoldingPlatform> AddHoldingPlatforms(IEnumerable<Building_HoldingPlatform> enumerable)
        {
            return enumerable.Concat(VehiclePawnWithMapCache.AllVehiclesOn(Find.CurrentMap).SelectMany(v => v.VehicleMap.listerBuildings.AllBuildingsColonistOfClass<Building_HoldingPlatform>()));
        }
    }

    //複数ポーンを選択してる時の行き先計算
    [HarmonyPatch(typeof(MultiPawnGotoController), "RecomputeDestinations")]
    public static class Patch_MultiPawnGotoController_RecomputeDestinations
    {
        public static void Prefix()
        {
            tmpEnterSpots.Clear();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }

        public static Dictionary<(Pawn pawn, IntVec3 dest), (TargetInfo exitSpot, TargetInfo enterSpot)> tmpEnterSpots = new Dictionary<(Pawn pawn, IntVec3 dest), (TargetInfo, TargetInfo)>();
    }

    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.ProcessInputEvents))]
    public static class Patch_MultiPawnGotoController_ProcessInputEvents
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.Draw))]
    public static class Patch_MultiPawnGotoController_Draw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ToVector3ShiftedWithAltitude = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), new Type[] { typeof(float) });
            var m_ToVector3ShiftedOffsetWithAltitude = AccessTools.Method(typeof(Patch_MultiPawnGotoController_Draw), nameof(ToVector3ShiftedOffsetWithAltitude));
            var m_Fogged = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new Type[] { typeof(IntVec3), typeof(Map) });
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
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue((pawn, intVec), out var spots))
            {
                var destMap = spots.enterSpot.Map ?? spots.exitSpot.Map.BaseMap() ?? pawn.Map;
                if (destMap.IsNonFocusedVehicleMapOf(out var vehicle))
                {
                    return intVec.ToVector3Shifted().ToBaseMapCoord(vehicle).WithY(AddedAltitude);
                }
            }
            return intVec.ToVector3ShiftedWithAltitude(AddedAltitude);
        }

        private static bool FoggedOffset(IntVec3 intVec, Pawn pawn)
        {
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue((pawn, intVec), out var spots))
            {
                var destMap = spots.enterSpot.Map ?? spots.exitSpot.Map.BaseMap() ?? pawn.Map;
                if (destMap.IsNonFocusedVehicleMapOf(out var vehicle))
                {
                    return intVec.ToBaseMapCoord(vehicle).Fogged(destMap);
                }
            }
            return intVec.Fogged(pawn.Map);
        }
    }

    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.OnGUI))]
    public static class Patch_MultiPawnGotoController_OnGUI
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ToUIRect = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToUIRect));
            var m_ToUIRectOffset = AccessTools.Method(typeof(Patch_MultiPawnGotoController_OnGUI), nameof(ToUIRectOffset));
            var m_Fogged = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new Type[] { typeof(IntVec3), typeof(Map) });
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
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue((pawn, intVec), out var spots))
            {
                var destMap = spots.enterSpot.Map ?? spots.exitSpot.Map.BaseMap() ?? pawn.Map;
                if (destMap.IsNonFocusedVehicleMapOf(out var vehicle))
                {
                    return Ext_Math.RotatePoint(intVec.ToVector3(), intVec.ToVector3Shifted(), vehicle.FullRotation.AsAngle).ToBaseMapCoord(vehicle);
                }
            }
            return intVec.ToVector3();
        }
    }

    //終わったらキャッシュをクリア
    [HarmonyPatch(typeof(MultiPawnGotoController), "IssueGotoJobs")]
    public static class Patch_MultiPawnGotoController_IssueGotoJobs
    {
        public static void Postfix()
        {
            Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.Clear();
        }
    }

    //行き先がVehicleMap上にあると登録されているかsearcherがVehicleMap上に居る時はBestOrderedGotoDestNearを置き換え
    //ジャンプ時のTargetVehicleも考慮にいれるよう変更
    [HarmonyPatch(typeof(RCellFinder), nameof(RCellFinder.BestOrderedGotoDestNear))]
    public static class Patch_RCellFinder_BestOrderedGotoDestNear
    {
        public static bool Prefix(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, ref IntVec3 __result)
        {
            VehiclePawnWithMap vehicle = null;
            if (TargetMapManager.HasTargetMap(searcher, out var map))
            {
                __result = ReachabilityUtilityOnVehicle.BestOrderedGotoDestNear(root, searcher, cellValidator, map, out var exitSpot, out var enterSpot);
                if (__result.IsValid)
                {
                    if (Find.Selector.gotoController.Active)
                    {
                        Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots[(searcher, __result)] = (exitSpot, enterSpot);
                    }
                    return false;
                }
            }
            else if (root.InBounds(Find.CurrentMap) && root.TryGetVehicleMap(Find.CurrentMap, out vehicle) || searcher.IsOnNonFocusedVehicleMapOf(out _))
            {
                var dest = vehicle != null ? root.ToVehicleMapCoord(vehicle) : root;
                __result = ReachabilityUtilityOnVehicle.BestOrderedGotoDestNear(
                    dest,
                    searcher,
                    cellValidator,
                    vehicle != null ? vehicle.VehicleMap : Find.CurrentMap,
                    out var exitSpot,
                    out var enterSpot);
                if (__result.IsValid)
                {
                    Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots[(searcher, __result)] = (exitSpot, enterSpot);
                    return false;
                }
            }
            return true;
        }
    }

    //行き先がVehicleMap上にあると登録されているかsearcherがVehicleMap上に居る時はBestOrderedGotoDestNearの置き換えで登録されたspotsを使ってGotoAcrossMapsに誘導
    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnGotoAction))]
    public static class Patch_FloatMenuOptionProvider_DraftedMove_PawnGotoAction
    {
        public static bool Prefix(IntVec3 clickCell, Pawn pawn, IntVec3 gotoLoc)
        {
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue((pawn, gotoLoc), out var spots))
            {
                var destMap = spots.enterSpot.Map ?? spots.exitSpot.Map.BaseMap() ?? pawn.Map;
                if (destMap.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
                {
                    clickCell = clickCell.ToVehicleMapCoord(vehicle);
                }
                PawnGotoAction(clickCell, pawn, destMap, spots.exitSpot, spots.enterSpot, gotoLoc);
                return false;
            }
            return true;
        }

        public static void PawnGotoAction(IntVec3 clickCell, Pawn pawn, Map map, TargetInfo dest1, TargetInfo dest2, LocalTargetInfo dest3)
        {
            bool flag;
            var baseMap = map.BaseMap();
            if (!dest1.IsValid && !dest2.IsValid && pawn.Map == map && pawn.Position == dest3.Cell)
            {
                flag = true;
                if (pawn.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            }
            else if (pawn.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps && pawn.Map == map && pawn.CurJob.targetA == dest3)
            {
                flag = true;
            }
            else
            {
                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, dest3).SetSpotsToJobAcrossMaps(pawn, dest1, dest2);
                if (pawn.Map == baseMap && baseMap.exitMapGrid.IsExitCell(clickCell))
                {
                    job.exitMapOnArrival = !pawn.IsColonyMech;
                }
                else if (!baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid && pawn.Map == baseMap && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3) && baseMap.Parent.GetComponent<FormCaravanComp>() != null && MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + baseMap.uniqueID, 60f))
                {
                    if (baseMap.Parent.GetComponent<FormCaravanComp>().CanFormOrReformCaravanNow)
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate(), baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(), baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                }
                flag = pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
            }
            if (flag)
            {
                FleckMaker.Static(dest3.Cell, map, FleckDefOf.FeedbackGoto, 1f);
            }
        }
    }
}