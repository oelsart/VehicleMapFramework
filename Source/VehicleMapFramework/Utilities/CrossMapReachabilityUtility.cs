using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class CrossMapReachabilityUtility
{
    public static bool working;

    public static Map DestMap;

    public static Map DepartMap;

#if DEBUG
    public static bool enableDebugLog;
#endif

    [Conditional("DEBUG")]
    internal static void DebugLog(string message)
    {
#if DEBUG
        if (!enableDebugLog) return;
#endif
        Log.Message($"[CrossMapReachability] {message}");
    }

    public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, VehiclePawn enterer = null)
    {
        return EnterVehiclePosition(enterSpot, out _, enterer);
    }

    public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, out int dist, VehiclePawn enterer = null)
    {
        if (!enterSpot.Map.IsVehicleMapOf(out var vehicle) || (!vehicle?.Spawned ?? true))
        {
            dist = 0;
            return IntVec3.Invalid;
        }

        var cell = enterSpot.Cell.ToBaseMapCoord(vehicle);
        IntVec3 faceCell;
        if (enterSpot.HasThing)
        {
            faceCell = enterSpot.Thing.BaseFullRotation().FacingCell;
        }
        else
        {
            faceCell = enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).FacingCell;
        }

        dist = 1;
        while ((cell - (faceCell * dist)).GetThingList(vehicle.Map).Contains(vehicle))
        {
            dist++;
        }
        if (enterSpot.Thing is Building_VehicleRamp && dist < 2) dist++;

        var result = cell - (faceCell * dist);
        if (enterer != null)
        {
            result -= faceCell * enterer.HalfLength();
        }
        return result;
    }

    public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParms, Map destMap)
    {
        return CanReach(departMap, root, dest, peMode, traverseParms, destMap, out _, out _);
    }

    public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (working)
        {
            Log.ErrorOnce("Called CanReach() while working. This should never happen. Suppressing further errors.", 7312233);
            return false;
        }

        if (traverseParms.pawn is VehiclePawn vehiclePawn)
        {
            return vehiclePawn.CanReachVehicle(dest, peMode, traverseParms.maxDanger, traverseParms.mode, destMap, out exitSpot, out enterSpot);
        }

        if (CrossMapReachabilityCache.TryGetCache(new TargetInfo(root, departMap), dest.ToTargetInfo(destMap), traverseParms, out var result, out exitSpot, out enterSpot))
        {
            DebugLog($"Result from cache: {root}, {departMap}, {dest}, {destMap}, {traverseParms}: {result}, {exitSpot}, {enterSpot}");
            return result;
        }
        working = true;
        try
        {

            if (departMap == null || destMap == null) return false;

            if (departMap == destMap)
            {
                result = destMap.reachability.CanReach(root, dest, peMode, traverseParms);
                DebugLog($"departMap == destMap: {result}");
                return result;
            }
            var destBaseMap = destMap.IsVehicleMapOf(out var vehicle) && vehicle.Spawned ? vehicle.Map : destMap;
            var departBaseMap = departMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : departMap;

            if (departBaseMap == destBaseMap)
            {
                var flag = departMap == departBaseMap;
                var flag2 = departBaseMap == destMap;
                var traverseParms2 = traverseParms.pawn != null ?
                    TraverseParms.For(traverseParms.pawn, traverseParms.maxDanger, TraverseMode.PassDoors, traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger) :
                    TraverseParms.For(TraverseMode.PassDoors, traverseParms.maxDanger, traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger);

                bool CanReach(IntVec3 cell, IntVec3 cell2)
                {
                    return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                        destMap.reachability.CanReach(cell2, dest, peMode, traverseParms);
                }
                //出発地が車上マップで目的地がベースマップ
                if (!flag && flag2)
                {
                    if (vehicle2 != null)
                    {
                        if (!vehicle2.AllowExitFor(traverseParms.pawn))
                        {
                            result = false;
                            return result;
                        }
                        foreach (var comp in vehicle2.AvailableEnterComps.OrderBy(e => e.DistanceSquared(dest.Cell)))
                        {
                            IntVec3 cell;
                            if (comp is CompZipline compZipline)
                            {
                                var pair = compZipline.Pair;
                                if (pair == null || !pair.HasComp<CompZipline>() || pair.Map != destMap) continue;

                                cell = pair.Position;
                            }
                            else
                            {
                                cell = EnterVehiclePosition(comp.parent);
                            }
                            result = cell.Standable(destMap) && CanReach(comp.parent.Position, cell);
                            DebugLog($"VehicleMap => BaseMap: {root}, {cell}, {comp}, {traverseParms} :{result} {comp.parent}");
                            if (result)
                            {
                                exitSpot = comp.parent;
                                return result;
                            }
                        }
                        foreach (var c in vehicle2.CachedStandableMapEdgeCells.OrderBy(c => (c.ToBaseMapCoord(vehicle2) - dest.Cell).LengthHorizontalSquared))
                        {
                            var targetInfo = new TargetInfo(c, departMap);
                            var cell = EnterVehiclePosition(targetInfo);
                            result = cell.Standable(destMap) && CanReach(c, cell);
                            DebugLog($"VehicleMap => BaseMap: {root}, {cell}, {c}, {traverseParms} :{result} {targetInfo}");
                            if (result)
                            {
                                exitSpot = targetInfo;
                                return result;
                            }
                        }
                    }
                }
                //出発地がベースマップで目的地が車上マップ
                else if (flag && !flag2)
                {
                    if (vehicle != null)
                    {
                        if (!vehicle.AllowEnterFor(traverseParms.pawn))
                        {
                            result = false;
                            return result;
                        }
                        foreach (var comp in vehicle.EnterComps.OrderBy(e => e.DistanceSquared(root)))
                        {
                            IntVec3 cell;
                            if (comp is CompZipline compZipline)
                            {
                                var pair = compZipline.Pair;
                                if (pair == null || !pair.HasComp<CompZipline>() || pair.Map != departMap) continue;

                                cell = pair.Position;
                            }
                            else
                            {
                                cell = EnterVehiclePosition(comp.parent);
                            }

                            result = cell.Standable(departMap) && CanReach(cell, comp.parent.Position);
                            DebugLog($"BaseMap => VehicleMap: {root}, {cell}, {comp}, {traverseParms} :{result}");
                            if (result)
                            {
                                enterSpot = comp.parent;
                                return result;
                            }
                        }
                        foreach (var c in vehicle.CachedStandableMapEdgeCells.OrderBy(c => (root - c.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                        {
                            var targetInfo = new TargetInfo(c, destMap);
                            var cell = EnterVehiclePosition(targetInfo);
                            result = cell.Standable(departMap) && CanReach(cell, c);
                            DebugLog($"BaseMap => VehicleMap: {new TargetInfo(root, departMap)}, {cell}, {c}, {dest.ToTargetInfo(destMap)}, {traverseParms} :{result}");
                            if (result)
                            {
                                enterSpot = targetInfo;
                                return result;
                            }
                        }
                    }
                }
                //出発地と目的地がそれぞれ別の車上マップ
                else
                {
                    if (vehicle2 != null)
                    {
                        if (!vehicle2.AllowExitFor(traverseParms.pawn))
                        {
                            result = false;
                            return result;
                        }

                        if (vehicle != null)
                        {
                            if (!vehicle.AllowEnterFor(traverseParms.pawn))
                            {
                                result = false;
                                return result;
                            }

                            bool CanReach2(IntVec3 cell, IntVec3 cell2, IntVec3 cell3, IntVec3 cell4)
                            {
                                return cell2.Standable(departBaseMap) &&
                                    cell3.Standable(departBaseMap) &&
                                    departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                                    departBaseMap.reachability.CanReach(cell2, cell3, PathEndMode.OnCell, traverseParms2) &&
                                    destMap.reachability.CanReach(cell4, dest, peMode, traverseParms2);
                            }

                            foreach (var comp in vehicle2.AvailableEnterComps.OrderBy(e => e.DistanceSquared(dest.Cell.ToBaseMapCoord(vehicle))))
                            {
                                IntVec3 cell;
                                if (comp is CompZipline compZipline)
                                {
                                    var pair = compZipline.Pair;
                                    if (pair == null || !pair.HasComp<CompZipline>() || pair.Map == departMap) continue;

                                    cell = pair.Position;

                                    //departMapからdestMapまで直通のジップラインがある場合
                                    if (pair.Map == destMap)
                                    {
                                        var c = comp.parent.Position;
                                        if (cell.Standable(destMap) && CanReach(c, cell))
                                        {
                                            exitSpot = comp.parent;
                                            enterSpot = pair;
                                            result = true;
                                            return result;
                                        }
                                    }
                                }
                                else
                                {
                                    cell = EnterVehiclePosition(comp.parent);
                                }
                                foreach (var comp2 in vehicle.AvailableEnterComps.OrderBy(e => e.DistanceSquared(cell)))
                                {
                                    IntVec3 cell2;
                                    if (comp2 is CompZipline compZipline2)
                                    {
                                        var pair = compZipline2.Pair;
                                        if (pair == null || pair.Isnt<ZiplineEnd>() || pair.Map != departBaseMap) continue;
                                        cell2 = pair.Position;
                                    }
                                    else
                                    {
                                        cell2 = EnterVehiclePosition(comp2.parent);
                                    }
                                    if (CanReach2(comp.parent.Position, cell, cell2, comp2.parent.Position))
                                    {
                                        exitSpot = comp.parent;
                                        enterSpot = comp2.parent;
                                        result = true;
                                        return result;
                                    }
                                }
                                foreach (var c2 in vehicle.CachedStandableMapEdgeCells.OrderBy(c2 => cell - c2.ToBaseMapCoord(vehicle)))
                                {
                                    var targetInfo = new TargetInfo(c2, destMap);
                                    var cell2 = EnterVehiclePosition(targetInfo);
                                    if (CanReach2(comp.parent.Position, cell, cell2, c2))
                                    {
                                        exitSpot = comp.parent;
                                        enterSpot = targetInfo;
                                        result = true;
                                        return result;
                                    }
                                }
                            }
                            foreach (var c in vehicle2.CachedStandableMapEdgeCells.OrderBy(c => (c.ToBaseMapCoord(vehicle2) - dest.Cell.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                            {
                                var targetInfo = new TargetInfo(c, departMap);
                                var cell = EnterVehiclePosition(targetInfo);

                                foreach (var comp2 in vehicle.AvailableEnterComps.OrderBy(e => e.DistanceSquared(cell)))
                                {
                                    IntVec3 cell2;
                                    if (comp2 is CompZipline compZipline)
                                    {
                                        var pair = compZipline.Pair;
                                        if (pair == null || pair.Isnt<ZiplineEnd>() || pair.Map != departBaseMap) continue;
                                        cell2 = pair.Position;
                                    }
                                    else
                                    {
                                        cell2 = EnterVehiclePosition(comp2.parent);
                                    }
                                    if (CanReach2(c, cell, cell2, comp2.parent.Position))
                                    {
                                        exitSpot = targetInfo;
                                        enterSpot = comp2.parent;
                                        result = true;
                                        return result;
                                    }
                                }
                                foreach (var c2 in vehicle.CachedStandableMapEdgeCells.OrderBy(c2 => (cell - c2.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                                {
                                    var targetInfo2 = new TargetInfo(c2, destMap);
                                    var cell2 = EnterVehiclePosition(targetInfo2);
                                    if (CanReach2(c, cell, cell2, c2))
                                    {
                                        exitSpot = targetInfo;
                                        enterSpot = targetInfo2;
                                        result = true;
                                        return result;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            result = false;
            return result;
        }
        finally
        {
            CrossMapReachabilityCache.Cache(new TargetInfo(root, departMap), dest.ToTargetInfo(destMap), traverseParms, result, exitSpot, enterSpot);
            working = false;
#if DEBUG
            enableDebugLog = false;
#endif
        }

    }

    public static bool CanReach(this Pawn pawn, LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode, Map destMap, out TargetInfo dest1, out TargetInfo dest2)
    {
        var traverseParms = TraverseParms.For(pawn, maxDanger: maxDanger, mode: mode, canBashDoors: canBashDoors, canBashFences: canBashFences);
        dest1 = TargetInfo.Invalid;
        dest2 = TargetInfo.Invalid;
        return pawn.Spawned && CanReach(pawn.Map, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out dest1, out dest2);
    }

    public static bool GetClosestExitEnterSpot(Map departMap, IntVec3 root, TraverseParms traverseParms, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        var flag = departMap.IsVehicleMapOf(out var vehicle);
        var flag2 = destMap.IsVehicleMapOf(out var vehicle2);
        if (!flag && !flag2 && departMap != destMap)
        {
            return false;
        }
        if (departMap.BaseMap() != destMap.BaseMap())
        {
            return false;
        }

        var tmpExitSpot = TargetInfo.Invalid;
        var tmpEnterSpot = TargetInfo.Invalid;
        if (flag2)
        {
            if (vehicle2.EnterComps.Any(c => CanReach(departMap, root, c.parent, PathEndMode.OnCell, traverseParms, destMap, out tmpExitSpot, out tmpEnterSpot)))
            {
                exitSpot = tmpExitSpot;
                enterSpot = tmpEnterSpot;
                return true;
            }
            if (vehicle2.CachedStandableMapEdgeCells.Any(c => CanReach(departMap, root, c, PathEndMode.OnCell, traverseParms, destMap, out tmpExitSpot, out tmpEnterSpot)))
            {
                exitSpot = tmpExitSpot;
                enterSpot = tmpEnterSpot;
                return true;
            }
        }
        else if (flag)
        {
            if (vehicle.EnterComps.Any(c => CanReach(departMap, root, c.EnterVehiclePosition, PathEndMode.OnCell, traverseParms, destMap, out tmpExitSpot, out tmpEnterSpot)))
            {
                exitSpot = tmpExitSpot;
                enterSpot = tmpEnterSpot;
                return true;
            }
            if (vehicle.CachedStandableMapEdgeCells.Any(c => CanReach(departMap, root, EnterVehiclePosition(new TargetInfo(c, departMap)), PathEndMode.OnCell, traverseParms, destMap, out tmpExitSpot, out tmpEnterSpot)))
            {
                exitSpot = tmpExitSpot;
                enterSpot = tmpEnterSpot;
                return true;
            }
        }
        return false;
    }

    public static IntVec3 BestOrderedGotoDestNear(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, Map map, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (IsGoodDest(root, searcher, cellValidator, map, out exitSpot, out enterSpot))
        {
            return root;
        }
        int num = 1;
        IntVec3 result = default;
        float num2 = -1000f;
        bool flag = false;
        int num3 = GenRadial.NumCellsInRadius(30f);
        do
        {
            IntVec3 intVec = root + GenRadial.RadialPattern[num];
            if (IsGoodDest(intVec, searcher, cellValidator, map, out exitSpot, out enterSpot))
            {
                float num4 = CoverUtility.TotalSurroundingCoverScore(intVec, map);
                if (num4 > num2)
                {
                    num2 = num4;
                    result = intVec;
                    flag = true;
                }
            }
            if (num >= 8 && flag)
            {
                return result;
            }
            num++;
        }
        while (num < num3);
        return searcher.Position;
    }

    public static bool IsGoodDest(IntVec3 c, Pawn searcher, Predicate<IntVec3> cellValidator, Map map, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (cellValidator != null && !cellValidator(c))
        {
            return false;
        }

        if (!map.pawnDestinationReservationManager.CanReserve(c, searcher, true) || !searcher.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out exitSpot, out enterSpot))
        {
            return false;
        }
        if (!c.Standable(map))
        {
            Building_Door door = c.GetDoor(map);
            if (door == null || !door.CanPhysicallyPass(searcher))
            {
                return false;
            }
        }
        List<Thing> thingList = c.GetThingList(map);
        for (int i = 0; i < thingList.Count; i++)
        {
            if (thingList[i] is Pawn pawn && pawn != searcher && pawn.RaceProps.Humanlike && ((searcher.Faction == Faction.OfPlayer && pawn.Faction == searcher.Faction) || (searcher.Faction != Faction.OfPlayer && pawn.Faction != Faction.OfPlayer)))
            {
                return false;
            }
        }
        return true;
    }

    public static bool CanReachVehicle(this VehiclePawn vehicle, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, TraverseMode mode, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;

        var traverseParms = TraverseParms.For(vehicle, maxDanger, mode, false, false, false);
        bool result = false;
        try
        {
            if (CrossMapReachabilityCache.TryGetCache(new TargetInfo(vehicle.Position, vehicle.Map), dest.ToTargetInfo(destMap), traverseParms, out result, out exitSpot, out enterSpot))
            {
                return result;
            }
            if (dest.Cell == vehicle.Position && destMap == vehicle.Map)
            {
                return true;
            }
            if (!vehicle.Spawned) return false;

            var departMap = vehicle.Map;
            if (departMap == null || destMap == null) return false;
            if (departMap == destMap)
            {
                return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, dest, peMode, traverseParms);
            }
            if (vehicle is VehiclePawnWithMap)
            {
                return false;
            }
            var destBaseMap = destMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : destMap;
            var departBaseMap = departMap.IsVehicleMapOf(out var vehicle3) && vehicle3.Spawned ? vehicle3.Map : departMap;

            //行き先のマップでまだPathGridが作られてない場合構築をリクエストする処理を追加
            var destMapPathing = MapComponentCache<VehiclePathingSystem>.GetComponent(destMap);
            if (!destMapPathing[vehicle.VehicleDef].VehiclePathGrid.Enabled)
            {
                destMapPathing.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
            }

            if (departBaseMap == destBaseMap)
            {
                var flag = departMap == departBaseMap;
                var flag2 = departBaseMap == destMap;
                bool AvailableEnterSpot(CompVehicleEnterSpot comp)
                {
                    return comp != null && comp.Props.allowPassingVehicle && comp.parent.def.size.x >= vehicle.VehicleDef.size.x;
                }

                //vehicleが車上マップに居て目的地がベースマップ
                if (!flag && flag2)
                {
                    if (vehicle3 != null)
                    {

                        Thing tmpSpot = null;
                        result = vehicle3.EnterComps.Where(e => e.Isnt<CompZipline>()).OrderBy(e => e.DistanceSquared(dest.Cell)).Any(e =>
                        {
                            tmpSpot = e.parent;
                            if (!AvailableEnterSpot(e) || tmpSpot.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, departMap))) return false;

                            var cell = EnterVehiclePosition(tmpSpot, vehicle);
                            return vehicle.VehicleDef.CellRectStandable(destMap, cell, tmpSpot.BaseFullRotation().Opposite) &&
                                MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, tmpSpot, PathEndMode.OnCell, traverseParms) &&
                                destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
                        });
                        exitSpot = result ? tmpSpot : TargetInfo.Invalid;
                        return result;
                    }
                }
                //vehicleがベースマップに居て目的地が車上マップ
                else if (flag && !flag2)
                {
                    if (vehicle2 != null)
                    {
                        Thing tmpSpot = null;
                        result = vehicle2.EnterComps.Where(e => e.Isnt<CompZipline>()).OrderBy(e => e.DistanceSquared(vehicle.Position)).Any(e =>
                        {
                            tmpSpot = e.parent;
                            if (!AvailableEnterSpot(e) || tmpSpot.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, destMap))) return false;

                            var cell = EnterVehiclePosition(tmpSpot, vehicle);
                            var cell2 = tmpSpot.Position + (tmpSpot.Rotation.FacingCell * vehicle.HalfLength());
                            return vehicle.VehicleDef.CellRectStandable(destMap, cell2, tmpSpot.Rotation) &&
                                MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                                destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
                        });
                        enterSpot = result ? tmpSpot : TargetInfo.Invalid;
                        return result;
                    }
                }
                //vehicleと目的地がそれぞれ別の車上マップ
                else
                {
                    if (vehicle3 != null)
                    {
                        if (vehicle2 != null)
                        {
                            //行き先のベースマップでまだPathGridが作られてない場合構築をリクエストする処理を追加
                            var departBaseMapPathing = MapComponentCache<VehiclePathingSystem>.GetComponent(departBaseMap);
                            if (!departBaseMapPathing[vehicle.VehicleDef].VehiclePathGrid.Enabled)
                            {
                                departBaseMapPathing.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
                            }

                            Thing tmpSpot = null;
                            Thing tmpSpot2 = null;
                            result = vehicle3.EnterComps.Where(e => e.Isnt<CompZipline>()).OrderBy(e => e.DistanceSquared(dest.Cell.ToBaseMapCoord(vehicle2))).Any(e =>
                            {
                                tmpSpot = e.parent;
                                if (!AvailableEnterSpot(e) || tmpSpot.OccupiedRect().Any(c => !vehicle.Drivable(c, departMap))) return false;

                                var cell = EnterVehiclePosition(tmpSpot, vehicle);

                                return vehicle2.EnterComps.Where(e2 => e2.Isnt<CompZipline>()).OrderBy(e2 => e2.DistanceSquared(cell)).Any(e2 =>
                                {
                                    tmpSpot2 = e2.parent;
                                    if (!AvailableEnterSpot(e2) || tmpSpot2.OccupiedRect().Any(c => !vehicle.Drivable(c, destMap))) return false;

                                    var cell2 = EnterVehiclePosition(tmpSpot2, vehicle);
                                    var cell3 = tmpSpot2.Position + (tmpSpot2.Rotation.FacingCell * vehicle.HalfLength());

                                    return vehicle.VehicleDef.CellRectStandable(departBaseMap, cell, tmpSpot.BaseFullRotation().Opposite) &&
                                        vehicle.VehicleDef.CellRectStandable(destMap, cell3, tmpSpot2.Rotation) &&
                                        MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, tmpSpot, PathEndMode.OnCell, traverseParms) &&
                                        departBaseMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, cell2, PathEndMode.OnCell, TraverseMode.PassDoors, traverseParms.maxDanger) &&
                                        destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell3, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
                                });
                            });
                            exitSpot = result ? tmpSpot : TargetInfo.Invalid;
                            enterSpot = result ? tmpSpot2 : TargetInfo.Invalid;
                            return result;
                        }
                    }
                }
            }
            result = false;
            return false;
        }
        finally
        {
            CrossMapReachabilityCache.Cache(new TargetInfo(vehicle.Position, vehicle.Map), dest.ToTargetInfo(destMap), traverseParms, result, exitSpot, enterSpot);
        }
    }

    public static bool TryFindNearestStandableCell(VehiclePawn vehicle, IntVec3 cell, Map map, out IntVec3 result, float radius = -1f)
    {
        if (radius < 0f)
        {
            radius = Mathf.Min(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z) * 2;
        }

        int num = GenRadial.NumCellsInRadius(radius);
        result = IntVec3.Invalid;
        for (int i = 0; i < num; i++)
        {
            IntVec3 intVec = GenRadial.RadialPattern[i] + cell;
            if (intVec.Standable(vehicle, map) && (!VehicleMod.settings.main.fullVehiclePathing || vehicle.DrivableRectOnCell(intVec, true, map)))
            {
                if (map == vehicle.Map && intVec == vehicle.Position || vehicle.beached)
                {
                    result = intVec;
                    return true;
                }

                if (AnyVehicleBlockingPathAt(intVec, vehicle, map) == null && vehicle.CanReachVehicle(intVec, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn, map, out _, out _))
                {
                    result = intVec;
                    return true;
                }
            }
        }

        return false;
    }

    public static VehiclePawn AnyVehicleBlockingPathAt(IntVec3 cell, VehiclePawn vehicle, Map map)
    {
        List<Thing> thingList = cell.GetThingList(map);
        if (thingList.NullOrEmpty()) return null;

        float euclideanDistance = Ext_Map.Distance(vehicle.PositionOnBaseMap(), cell.ToBaseMapCoord(map));
        for (int i = 0; i < thingList.Count; i++)
        {
            if (thingList[i] is VehiclePawn otherVehicle && otherVehicle != vehicle)
            {
                if (euclideanDistance < 20 || !otherVehicle.vehiclePather.Moving)
                {
                    return otherVehicle;
                }
            }
        }

        return null;
    }

    public static bool DrivableRectOnCell(this VehiclePawn vehicle, IntVec3 cell, bool maxPossibleSize, Map map)
    {
        if (maxPossibleSize)
        {
            if (!vehicle.VehicleRect(cell, Rot8.North).All(rectCell => vehicle.Drivable(rectCell, map)))
            {
                return false;
            }

            return vehicle.VehicleRect(cell, Rot8.East).All(rectCell => vehicle.Drivable(rectCell, map));
        }

        return vehicle.MinRect(cell).Cells.All(c => vehicle.Drivable(c, map));
    }

    public static bool Drivable(this VehiclePawn vehicle, IntVec3 cell, Map map)
    {
        if (cell.InBounds(map))
        {
            return vehicle.DrivableFast(cell, map);
        }

        return false;
    }

    public static bool DrivableFast(this VehiclePawn vehicle, int index, Map map)
    {
        IntVec3 cell = vehicle.Map.cellIndices.IndexToCell(index);
        return vehicle.DrivableFast(cell, map);
    }

    public static bool DrivableFast(this VehiclePawn vehicle, int x, int z, Map map)
    {
        IntVec3 cell = new(x, 0, z);
        return vehicle.DrivableFast(cell, map);
    }

    public static bool DrivableFast(this VehiclePawn vehicle, IntVec3 cell, Map map)
    {
        VehiclePawn vehiclePawn = map.GetDetachedMapComponent<VehiclePositionManager>().ClaimedBy(cell);
        if (vehiclePawn == null || vehiclePawn == vehicle)
        {
            return map.GetCachedMapComponent<VehiclePathingSystem>()[vehicle.VehicleDef].VehiclePathGrid.WalkableFast(cell);
        }

        return false;
    }
}
