using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class ReachabilityUtilityOnVehicle
    {
        public static bool working;

        public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, VehiclePawn enterer = null)
        {
            return EnterVehiclePosition(enterSpot, out _, enterer);
        }

        public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, out int dist, VehiclePawn enterer = null)
        {
            if (!enterSpot.Map.IsVehicleMapOf(out var vehicle))
            {
                Log.ErrorOnce($"[VehicleMapFramework] Invalid TargetInfo: {enterSpot}.", 3516351);
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
            while ((cell - faceCell * dist).GetThingList(vehicle.Map).Contains(vehicle))
            {
                dist++;
            }
            if (enterSpot.Thing is Building_VehicleRamp && dist < 2) dist++;

            var result = cell - faceCell * dist;
            if (enterer != null)
            {
                result -= faceCell * vehicle.HalfLength();
            }
            return result;
        }

        public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            bool result = false;
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            try
            {
                if (working)
                {
                    Log.ErrorOnce("Called CanReach() while working. This should never happen. Suppressing further errors.", 7312233);
                    return false;
                }
                working = true;

                if (traverseParms.pawn is VehiclePawn vehiclePawn)
                {
                    return vehiclePawn.CanReachVehicle(dest, peMode, traverseParms.maxDanger, traverseParms.mode, destMap, out exitSpot, out enterSpot);
                }

                if (departMap == null || destMap == null) return false;

                if (departMap == destMap)
                {
                    return destMap.reachability.CanReach(root, dest, peMode, traverseParms);
                }

                if (CrossMapReachabilityCache.TryGetCache(new TargetInfo(root, departMap), dest.ToTargetInfo(destMap), traverseParms, out result, out exitSpot, out enterSpot))
                {
                    return result;
                }

                var destBaseMap = destMap.IsVehicleMapOf(out var vehicle) && vehicle.Spawned ? vehicle.Map : destMap;
                var departBaseMap = departMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : departMap;

                if (departBaseMap == destBaseMap)
                {
                    var flag = departMap == departBaseMap;
                    var flag2 = departBaseMap == destMap;
                    var traverseParms2 = traverseParms.pawn != null ?
                        TraverseParms.For(traverseParms.pawn, traverseParms.maxDanger, TraverseMode.PassDoors, traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences) :
                        TraverseParms.For(TraverseMode.PassDoors, traverseParms.maxDanger, traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences);

                    bool CanReach(IntVec3 cell, IntVec3 cell2)
                    {
                        return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                            destMap.reachability.CanReach(cell2, dest, peMode, traverseParms2);
                    }
                    //出発地が車上マップで目的地がベースマップ
                    if (!flag && flag2)
                    {
                        if (vehicle2 != null)
                        {
                            if (!vehicle2.AllowsGetOff)
                            {
                                result = false;
                                return result;
                            }
                            foreach (var comp in vehicle2.StandableEnterComps.OrderBy(e => e.DistanceSquared(dest.Cell)))
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
                                if (cell.Standable(destMap) && CanReach(comp.parent.Position, cell))
                                {
                                    exitSpot = comp.parent;
                                    result = true;
                                    return result;
                                }
                            }
                            foreach (var c in vehicle2.CachedStandableMapEdgeCells.OrderBy(c => (c.ToBaseMapCoord(vehicle2) - dest.Cell).LengthHorizontalSquared))
                            {
                                var targetInfo = new TargetInfo(c, departMap);
                                var cell = EnterVehiclePosition(targetInfo);
                                if (cell.Standable(destMap) && CanReach(c, cell))
                                {
                                    exitSpot = targetInfo;
                                    result = true;
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
                                if (cell.Standable(departMap) && CanReach(cell, comp.parent.Position))
                                {
                                    enterSpot = comp.parent;
                                    result = true;
                                    return result;
                                }
                            }
                            foreach (var c in vehicle.CachedStandableMapEdgeCells.OrderBy(c => (root - c.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                            {
                                var targetInfo = new TargetInfo(c, destMap);
                                var cell = EnterVehiclePosition(targetInfo);
                                if (cell.Standable(departMap) && CanReach(cell, c))
                                {
                                    enterSpot = targetInfo;
                                    result = true;
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
                            if (!vehicle2.AllowsGetOff)
                            {
                                result = false;
                                return result;
                            }

                            if (vehicle != null)
                            {
                                bool CanReach2(IntVec3 cell, IntVec3 cell2, IntVec3 cell3, IntVec3 cell4)
                                {
                                    return cell2.Standable(departBaseMap) &&
                                        cell3.Standable(departBaseMap) &&
                                        departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                                        departBaseMap.reachability.CanReach(cell2, cell3, PathEndMode.OnCell, traverseParms2) &&
                                        destMap.reachability.CanReach(cell4, dest, peMode, traverseParms2);
                                }

                                foreach (var comp in vehicle2.StandableEnterComps.OrderBy(e => e.DistanceSquared(dest.Cell.ToBaseMapCoord(vehicle))))
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
                                    foreach (var comp2 in vehicle.StandableEnterComps.OrderBy(e => e.DistanceSquared(cell)))
                                    {
                                        IntVec3 cell2;
                                        if (comp2 is CompZipline compZipline2)
                                        {
                                            var pair = compZipline2.Pair;
                                            if (pair == null || pair.Isnt<ZiplineEnd>() || pair.Map != departBaseMap) return false;
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

                                    foreach (var comp2 in vehicle.StandableEnterComps.OrderBy(e => e.DistanceSquared(cell)))
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
            }

        }

        //public static bool CanReach(Map departMap, LocalTargetInfo dest3, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out LocalTargetInfo dest1, out LocalTargetInfo dest2)
        //{
        //    return ReachabilityUtilityOnVehicle.CanReach(departMap, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out dest1, out dest2);
        //}

        public static bool CanReach(this Pawn pawn, LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode, Map destMap, out TargetInfo dest1, out TargetInfo dest2)
        {
            var traverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors, false, canBashFences);
            dest1 = TargetInfo.Invalid;
            dest2 = TargetInfo.Invalid;
            return pawn.Spawned && ReachabilityUtilityOnVehicle.CanReach(pawn.Map, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out dest1, out dest2);
        }

        //置き換え用
        public static bool CanReach(Pawn pawn, LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode)
        {
            var traverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors, false, canBashFences);
            var destMap = ReachabilityUtilityOnVehicle.tmpDestMap ?? (dest3.HasThing ? dest3.Thing.MapHeld : pawn.BaseMap());
            return pawn.Spawned && ReachabilityUtilityOnVehicle.CanReach(pawn.Map, pawn.Position, dest3, peMode, traverseParms, destMap, out _, out _);
        }

        //Reachability.CanReachの代替にできるマップ間CanReach。departMapを先にtmpDepartMapにセットしておき、
        //destMapはthing.MapHeldか、reachability.mapと同じと推定する（VirtualMapTransferと組み合わせて使用することを想定しているため）。
        public static bool CanReachReplaceable(this Reachability reachability, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseMode traverseMode, Danger danger)
        {
            return reachability.CanReachReplaceable(start, dest, peMode, TraverseParms.For(traverseMode, danger));
        }

        public static bool CanReachReplaceable(this Reachability reachability, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParms)
        {
            var departMap = ReachabilityUtilityOnVehicle.tmpDepartMap ?? (traverseParms.pawn != null ? traverseParms.pawn.Map : map(reachability));
            var destMap = ReachabilityUtilityOnVehicle.tmpDestMap ?? (dest.HasThing ? dest.Thing.MapHeld : map(reachability));
            return ReachabilityUtilityOnVehicle.CanReach(departMap, start, dest, peMode, traverseParms, destMap, out _, out _);
        }

        public static Map tmpDestMap;

        public static Map tmpDepartMap;

        private static AccessTools.FieldRef<Reachability, Map> map = AccessTools.FieldRefAccess<Reachability, Map>("map");

        public static bool CanReachNonLocalReplaceable(this Reachability reachability, IntVec3 start, TargetInfo dest, PathEndMode peMode, TraverseParms traverseParms)
        {
            var departMap = map(reachability);
            var destMap = dest.Map;
            return ReachabilityUtilityOnVehicle.CanReach(departMap, start, (LocalTargetInfo)dest, peMode, traverseParms, destMap, out _, out _);
        }

        public static IntVec3 StandableCellNear(IntVec3 root, Map map, float radius, Predicate<IntVec3> validator, out Map destMap)
        {
            Map baseMap = map.BaseMap();
            if (root.TryGetFirstThing<VehiclePawnWithMap>(baseMap, out var vehicle))
            {
                var cell = root.ToVehicleMapCoord(vehicle);
                if (cell.InBounds(vehicle.VehicleMap))
                {
                    destMap = vehicle.VehicleMap;
                    return CellFinder.StandableCellNear(cell, destMap, radius, validator);
                }
            }
            destMap = baseMap;
            return CellFinder.StandableCellNear(root, baseMap, radius, validator);
        }

        public static IntVec3 BestOrderedGotoDestNear(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, Map map, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            if (ReachabilityUtilityOnVehicle.IsGoodDest(root, searcher, cellValidator, map, out exitSpot, out enterSpot))
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
                if (ReachabilityUtilityOnVehicle.IsGoodDest(intVec, searcher, cellValidator, map, out exitSpot, out enterSpot))
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
			for (int i = 0; i<thingList.Count; i++)
			{
                Pawn pawn;
                if ((pawn = (thingList[i] as Pawn)) != null && pawn != searcher && pawn.RaceProps.Humanlike && ((searcher.Faction == Faction.OfPlayer && pawn.Faction == searcher.Faction) || (searcher.Faction != Faction.OfPlayer && pawn.Faction != Faction.OfPlayer)))
                {
                    return false;
				}
			}
			return true;
		}

        public static bool TryFindBestExitSpot(Pawn carrier, Pawn pawn, out IntVec3 spot, out TargetInfo v_exitSpot, TraverseMode mode = TraverseMode.ByPawn, bool canBash = true)
        {
            var baseMap = pawn.BaseMap();
            if ((mode == TraverseMode.PassAllDestroyableThings || mode == TraverseMode.PassAllDestroyableThingsNotWater || mode == TraverseMode.PassAllDestroyablePlayerOwnedThings) && !carrier.CanReachMapEdge(pawn, out v_exitSpot))
            {
                TargetInfo exitSpot = TargetInfo.Invalid;
                if (RCellFinder.TryFindRandomPawnEntryCell(out spot, baseMap, 0f, true, (IntVec3 x) => ReachabilityUtilityOnVehicle.CanReach(pawn.Map, pawn.Position, x, PathEndMode.OnCell, TraverseParms.For(carrier), baseMap, out exitSpot, out _)))
                {
                    v_exitSpot = exitSpot;
                    return true;
                }
                return false;
            }
            int num = 0;
            int num2 = 0;
            IntVec3 intVec2;
            var positionOnBaseMap = pawn.PositionOnBaseMap();
            for (; ; )
            {
                num2++;
                if (num2 > 30)
                {
                    break;
                }
                IntVec3 intVec;
                bool flag = CellFinder.TryFindRandomCellNear(positionOnBaseMap, baseMap, num, null, out intVec, -1);
                num += 4;
                if (flag)
                {
                    int num3 = intVec.x;
                    intVec2 = new IntVec3(0, 0, intVec.z);
                    if (baseMap.Size.z - intVec.z < num3)
                    {
                        num3 = baseMap.Size.z - intVec.z;
                        intVec2 = new IntVec3(intVec.x, 0, baseMap.Size.z - 1);
                    }
                    if (baseMap.Size.x - intVec.x < num3)
                    {
                        num3 = baseMap.Size.x - intVec.x;
                        intVec2 = new IntVec3(baseMap.Size.x - 1, 0, intVec.z);
                    }
                    if (intVec.z < num3)
                    {
                        intVec2 = new IntVec3(intVec.x, 0, 0);
                    }
                    if (intVec2.Standable(baseMap) && ReachabilityUtilityOnVehicle.CanReach(pawn.Map, pawn.Position, intVec2, PathEndMode.OnCell, TraverseParms.For(carrier, Danger.Deadly, mode, canBash, false, canBash), baseMap, out v_exitSpot, out _))
                    {
                        goto Block_10;
                    }
                }
            }
            spot = pawn.Position;
            v_exitSpot = TargetInfo.Invalid;
            return false;
            Block_10:
            spot = intVec2;
            return true;
        }

        public static bool CanReachMapEdge(this Pawn carrier, Pawn pawn, out TargetInfo exitSpot)
        {
            exitSpot = TargetInfo.Invalid;
            if (!carrier.Spawned)
            {
                return false;
            }
            var traverseParms = TraverseParms.For(carrier);
            if (pawn.IsOnVehicleMapOf(out var vehicle))
            {
                var departMap = vehicle.VehicleMap;
                var destMap = pawn.BaseMap();
                bool CanReach(IntVec3 cell, IntVec3 cell2)
                {
                    return cell.Standable(departMap) &&
                        cell2.Standable(destMap) &&
                        departMap.reachability.CanReach(carrier.Position, cell, PathEndMode.OnCell, traverseParms) &&
                        destMap.reachability.CanReachMapEdge(cell2, traverseParms);
                }
                foreach (var comp in vehicle.EnterComps.OrderBy(e => e.DistanceSquared(pawn.PositionOnBaseMap())))
                {
                    IntVec3 cell;
                    if (comp is CompZipline compZipline)
                    {
                        var pair = compZipline.Pair;
                        if (pair == null || !pair.HasComp<CompZipline>() || pair.Map != destMap) return false;
                        cell = pair.Position;
                    }
                    else
                    {
                        cell = EnterVehiclePosition(comp.parent);
                    }
                    if (CanReach(comp.parent.Position, cell))
                    {
                        exitSpot = comp.parent;
                        return true;
                    }
                }
                foreach (var c in vehicle.CachedMapEdgeCells.OrderBy(c => (c - pawn.Position).LengthHorizontalSquared))
                {
                    var targetInfo = new TargetInfo(c, departMap);
                    var cell = EnterVehiclePosition(targetInfo);
                    if (CanReach(c, cell))
                    {
                        exitSpot = targetInfo;
                        return true;
                    }
                }
                return false;
            }
            return pawn.Map.reachability.CanReachMapEdge(pawn.Position, traverseParms);
        }

        public static bool CanReachVehicle(this VehiclePawn vehicle, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, TraverseMode mode, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            if (dest.Cell == vehicle.Position && destMap == vehicle.Map)
            {
                return true;
            }
            var traverseParms = TraverseParms.For(vehicle, maxDanger, mode, false, false, false);
            if (!vehicle.Spawned) return false;

            var departMap = vehicle.Map;
            if (departMap == null || destMap == null) return false;
            if (departMap == destMap)
            {
                return MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, dest, peMode, traverseParms);
            }
            var destBaseMap = destMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : destMap;
            var departBaseMap = departMap.IsVehicleMapOf(out var vehicle3) && vehicle3.Spawned ? vehicle3.Map : departMap;

            if (departBaseMap == destBaseMap)
            {
                if (vehicle is VehiclePawnWithMap)
                {
                    return false;
                }
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
                        var result = vehicle3.EnterComps.Where(e => e.Isnt<CompZipline>()).OrderBy(e => e.DistanceSquared(dest.Cell)).Any(e =>
                        {
                            tmpSpot = e.parent;
                            if (!AvailableEnterSpot(e) || tmpSpot.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, departMap))) return false;

                            var cell = EnterVehiclePosition(tmpSpot, vehicle);
                            return vehicle.VehicleDef.CellRectStandable(destMap, cell, tmpSpot.BaseFullRotation().Opposite) &&
                                MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, tmpSpot, PathEndMode.OnCell, traverseParms) &&
                                MapComponentCache<VehicleMapping>.GetComponent(destMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
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
                        var result = vehicle2.EnterComps.Where(e => e.Isnt<CompZipline>()).OrderBy(e => e.DistanceSquared(vehicle.Position)).Any(e =>
                        {
                            tmpSpot = e.parent;
                            if (!AvailableEnterSpot(e) || tmpSpot.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, destMap))) return false;

                            var cell = EnterVehiclePosition(tmpSpot, vehicle);
                            var cell2 = tmpSpot.Position + tmpSpot.Rotation.FacingCell * vehicle.HalfLength();
                            return vehicle.VehicleDef.CellRectStandable(destMap, cell2, tmpSpot.Rotation) &&
                                MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                                MapComponentCache<VehicleMapping>.GetComponent(destMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
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
                            Thing tmpSpot = null;
                            Thing tmpSpot2 = null;
                            var result = vehicle3.EnterComps.Where(e => e.Isnt<CompZipline>()).OrderBy(e => e.DistanceSquared(dest.Cell.ToBaseMapCoord(vehicle2))).Any(e =>
                            {
                                tmpSpot = e.parent;
                                if (!AvailableEnterSpot(e) || tmpSpot.OccupiedRect().Any(c => !vehicle.Drivable(c, departMap))) return false;

                                var cell = EnterVehiclePosition(tmpSpot, vehicle);

                                return vehicle2.EnterComps.Where(e2 => e2.Isnt<CompZipline>()).OrderBy(e2 => e2.DistanceSquared(cell)).Any(e2 =>
                                {
                                    tmpSpot2 = e2.parent;
                                    if (!AvailableEnterSpot(e2) || tmpSpot2.OccupiedRect().Any(c => !vehicle.Drivable(c, destMap))) return false;

                                    var cell2 = EnterVehiclePosition(tmpSpot2, vehicle);
                                    var cell3 = tmpSpot2.Position + tmpSpot2.Rotation.FacingCell * vehicle.HalfLength();

                                    return vehicle.VehicleDef.CellRectStandable(departBaseMap, cell, tmpSpot.BaseFullRotation().Opposite) &&
                                        vehicle.VehicleDef.CellRectStandable(destMap, cell3, tmpSpot2.Rotation) &&
                                        MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, tmpSpot, PathEndMode.OnCell, traverseParms) &&
                                        MapComponentCache<VehicleMapping>.GetComponent(departBaseMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, cell2, PathEndMode.OnCell, TraverseMode.PassDoors, traverseParms.maxDanger) &&
                                        MapComponentCache<VehicleMapping>.GetComponent(destMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell3, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
                                });
                            });
                            exitSpot = result ? tmpSpot : TargetInfo.Invalid;
                            enterSpot = result ? tmpSpot2 : TargetInfo.Invalid;
                            return result;
                        }
                    }
                }
            }
            return false;
        }

        public static bool DrivableRectOnCell(this VehiclePawn vehicle, IntVec3 cell, bool maxPossibleSize, Map map)
        {
            if (maxPossibleSize)
            {
                if (!vehicle.VehicleRect(cell, Rot8.North).All((IntVec3 rectCell) => vehicle.Drivable(rectCell, map)))
                {
                    return false;
                }

                return vehicle.VehicleRect(cell, Rot8.East).All((IntVec3 rectCell) => vehicle.Drivable(rectCell, map));
            }

            return vehicle.MinRect(cell).Cells.All((IntVec3 c) => vehicle.Drivable(c, map));
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
            IntVec3 cell = new IntVec3(x, 0, z);
            return vehicle.DrivableFast(cell, map);
        }

        public static bool DrivableFast(this VehiclePawn vehicle, IntVec3 cell, Map map)
        {
            VehiclePawn vehiclePawn = Ext_Map.GetCachedMapComponent<VehiclePositionManager>(map).ClaimedBy(cell);
            if (vehiclePawn == null || vehiclePawn == vehicle)
            {
                return MapComponentCache.GetCachedMapComponent<VehicleMapping>(map)[vehicle.VehicleDef].VehiclePathGrid.WalkableFast(cell);
            }

            return false;
        }
    }
}
