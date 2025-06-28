using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [HarmonyPatch(typeof(FloatMenuContext), MethodType.Constructor, typeof(List<Pawn>), typeof(Vector3), typeof(Map))]
    public static class Patch_FloatMenuContext_Constructor
    {
        public static void Prefix(List<Pawn> selectedPawns, ref Vector3 clickPosition, ref Map map)
        {
            if (clickPosition.TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
            {
                GenUIOnVehicle.vehicleForSelector = vehicle;
                clickPosition = clickPosition.ToVehicleMapCoord(vehicle);
                map = vehicle.VehicleMap;
            }
        }

        public static void Finalizer(FloatMenuContext __instance)
        {
            Pawn pawn;
            if (!__instance.IsMultiselect && (pawn = __instance.ValidSelectedPawns.FirstOrDefault()) != null)
            {
                TargetMapManager.TargetMap[pawn] = __instance.map;
            }
            GenUIOnVehicle.vehicleForSelector = null;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_ThingsUnderMouse = AccessTools.Method(typeof(GenUI), nameof(GenUI.ThingsUnderMouse));
            var m_ThingsUnderMouseOnVehicle = AccessTools.Method(typeof(GenUIOnVehicle), nameof(GenUIOnVehicle.ThingsUnderMouse), new[] { typeof(Vector3), typeof(float), typeof(TargetingParameters), typeof(ITargetingSource) });
            return instructions.MethodReplacer(m_ThingsUnderMouse, m_ThingsUnderMouseOnVehicle);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ShouldGenerateFloatMenuForPawn))]
    public static class Patch_FloatMenuMakerMap_ShouldGenerateFloatMenuForPawn
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_ExtinguishFires), "GetSingleOption")]
    public static class Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var g_FirstSelectedPawn = AccessTools.PropertyGetter(typeof(FloatMenuContext), nameof(FloatMenuContext.FirstSelectedPawn));
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(g_FirstSelectedPawn) && codes[i + 1].Calls(CachedMethodInfo.g_Thing_Map))
                {
                    codes.RemoveAt(i);
                    codes[i] = CodeInstruction.LoadField(typeof(FloatMenuContext), nameof(FloatMenuContext.map));
                }
            }
            return codes;
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
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingBaseMapCoord2)
            });
            return codes;
        }
    }

    //ベースマップに居る時のFloatMenuにもHoldingPlatform検索を足しときます
    [HarmonyPatch]
    public static class Patch_FloatMenuOptionProvider_Entity_GetOptionFor
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase GetOptionsFor_MoveNext(Type t)
            {
                if (!t.Name.Contains("<GetOptionsFor>")) return null;
                return AccessTools.Method(t, "MoveNext");
            }

            yield return AccessTools.FindIncludingInnerTypes(typeof(FloatMenuOptionProvider_CaptureEntity), GetOptionsFor_MoveNext);
            yield return AccessTools.FindIncludingInnerTypes(typeof(FloatMenuOptionProvider_TransferEntity), GetOptionsFor_MoveNext);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new CodeMatcher(instructions);
            var m_AllBuildingsColonistOfClass = AccessTools.Method(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass)).MakeGenericMethod(typeof(Building_HoldingPlatform));
            codes.MatchStartForward(CodeMatch.Calls(m_AllBuildingsColonistOfClass)).Advance(1);
            codes.Insert(CodeInstruction.Call(typeof(Patch_FloatMenuOptionProvider_Entity_GetOptionFor), nameof(AddHoldingPlatforms)));
            codes.MatchStartBackwards(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
            codes.Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing);

            var m_ClosestThing_Global_Reachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
            var m_ClosestThing_Global_ReachableOnVehicle = AccessTools.Method(typeof(GenClosestOnVehicle), nameof(GenClosestOnVehicle.ClosestThing_Global_Reachable),
                new[]
                {
                    typeof(IntVec3),
                    typeof(Map),
                    typeof(IEnumerable<Thing>),
                    typeof(PathEndMode),
                    typeof(TraverseParms),
                    typeof(float),
                    typeof(Predicate<Thing>),
                    typeof(Func<Thing,float>),
                    typeof(bool)
                });
            codes.MatchStartForward(CodeMatch.Calls(m_ClosestThing_Global_Reachable));
            codes.Operand = m_ClosestThing_Global_ReachableOnVehicle;
            return codes.Instructions();
        }

        private static IEnumerable<Building_HoldingPlatform> AddHoldingPlatforms(IEnumerable<Building_HoldingPlatform> enumerable)
        {
            return enumerable.Concat(VehiclePawnWithMapCache.AllVehiclesOn(Find.CurrentMap).SelectMany(v => v.VehicleMap.listerBuildings.AllBuildingsColonistOfClass<Building_HoldingPlatform>()));
        }
    }

    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.StartInteraction))]
    public static class Patch_MultiPawnGotoController_StartInteraction
    {
        public static void Prefix(ref IntVec3 mouseCell)
        {
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
            {
                mouseCell = mouseCell.ToBaseMapCoord(vehicle);
            }
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
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
        }

        public static Dictionary<Pawn, (TargetInfo exitSpot, TargetInfo enterSpot)> tmpEnterSpots = new Dictionary<Pawn, (TargetInfo, TargetInfo)>();
    }

    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.ProcessInputEvents))]
    public static class Patch_MultiPawnGotoController_ProcessInputEvents
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
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
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue(pawn, out var spots))
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
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue(pawn, out var spots))
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
            new StackTrace().GetFrame(1);
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue(pawn, out var spots))
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
                    Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots[searcher] = (exitSpot, enterSpot);
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
                    Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots[searcher] = (exitSpot, enterSpot);
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
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue(pawn, out var spots))
            {
                Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.Remove(pawn);
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

    [HarmonyPatch(typeof(FloatMenuOptionProvider_WorkGivers), "GetWorkGiversOptionsFor")]
    public static class Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiversOptionsFor
    {
        public static void Prefix(Pawn pawn, LocalTargetInfo target, FloatMenuContext context, ref object[] __state)
        {
            __state = new object[3];
            if (pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, context.map, out tmpExitSpot, out tmpEnterSpot))
            {
                __state[0] = true;
                __state[1] = pawn.Map;
                __state[2] = pawn.Position;
                pawn.VirtualMapTransfer(context.map, target.Cell);
                return;
            }
            __state[0] = false;
        }   

        public static void Finalizer(Pawn pawn, object[] __state)
        {
            if ((bool)__state[0])
            {
                pawn.VirtualMapTransfer((Map)__state[1], (IntVec3)__state[2]);
            }
        }

        public static TargetInfo tmpExitSpot;

        public static TargetInfo tmpEnterSpot;
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_WorkGivers), "GetWorkGiverOption")]
    public static class Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiverOption
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld && ((FieldInfo)c.operand).Name == "localJob");
            var job = generator.DeclareLocal(typeof(Job));

            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Stloc_S, job),
                CodeInstruction.LoadArgument(1),
                CodeInstruction.LoadField(typeof(Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiversOptionsFor), nameof(Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiversOptionsFor.tmpExitSpot)),
                CodeInstruction.LoadField(typeof(Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiversOptionsFor), nameof(Patch_FloatMenuOptionProvider_WorkGivers_GetWorkGiversOptionsFor.tmpEnterSpot)),
                new CodeInstruction(OpCodes.Ldloc_S, job),
                CodeInstruction.Call(typeof(JobAcrossMapsUtility), nameof(JobAcrossMapsUtility.GotoDestMapJob))
            });
            return codes;
        }
    }
}
