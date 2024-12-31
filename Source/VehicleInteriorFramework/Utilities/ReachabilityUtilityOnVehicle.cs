using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class ReachabilityUtilityOnVehicle
    {
        public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest3, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out TargetInfo dest1, out TargetInfo dest2)
        {
            dest1 = TargetInfo.Invalid;
            dest2 = TargetInfo.Invalid;
            if (departMap == null || destMap == null) return false;
            var destBaseMap = destMap.IsVehicleMapOf(out var vehicle) ? vehicle.Map : destMap;
            var departBaseMap = departMap.IsVehicleMapOf(out var vehicle2) ? vehicle2.Map : departMap;

            if (departBaseMap == destBaseMap)
            {
                if (departMap == destMap)
                {
                    return departMap.reachability.CanReach(root, dest3, peMode, traverseParms);
                }
                else
                {
                    var flag = departMap == departBaseMap;
                    var flag2 = departBaseMap == destMap;

                    //出発地が車上マップで目的地がベースマップ
                    if (!flag && flag2)
                    {
                        if (vehicle2 != null)
                        {
                            TargetInfo enterSpot = null;
                            var result = vehicle2.InteractionCells.OrderBy(c => (c.OrigToVehicleMap(vehicle2) - dest3.Cell).LengthHorizontalSquared).
                                Concat(CellRect.WholeMap(vehicle2.VehicleMap).EdgeCells.OrderBy(c => (c.OrigToVehicleMap(vehicle2) - dest3.Cell).LengthHorizontalSquared)).Any(c =>
                            {
                                enterSpot = c.GetThingList(departMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                IntVec3 basePos;
                                IntVec3 faceCell;
                                if (enterSpot != null)
                                {
                                    basePos = enterSpot.Thing.PositionOnBaseMap();
                                    faceCell = enterSpot.Thing.BaseFullRotation().FacingCell;
                                }
                                else
                                {
                                    enterSpot = new TargetInfo(c, departMap);
                                    basePos = enterSpot.Cell.OrigToVehicleMap(vehicle2);
                                    faceCell = enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle2.VehicleMap).FacingCell;
                                }
                                faceCell.y = 0;
                                var dist = 1;
                                while ((basePos - faceCell * dist).GetThingList(departBaseMap).Contains(vehicle2))
                                {
                                    dist++;
                                }
                                var cell = (basePos - faceCell * dist);
                                return departMap.reachability.CanReach(root, enterSpot.Cell, PathEndMode.OnCell, traverseParms) &&
                                cell.Standable(departBaseMap) &&
                                departBaseMap.reachability.CanReach(cell, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest1 = result ? enterSpot : TargetInfo.Invalid;

                            return result;
                        }
                    }
                    //出発地がベースマップで目的地が車上マップ
                    else if (flag && !flag2)
                    {
                        if (vehicle != null)
                        {
                            TargetInfo enterSpot = null;
                            var result = vehicle.InteractionCells.OrderBy(c => (root - c.OrigToVehicleMap(vehicle)).LengthHorizontalSquared)
                                .Concat(CellRect.WholeMap(vehicle.VehicleMap).EdgeCells.OrderBy(c => (root - c.OrigToVehicleMap(vehicle)).LengthHorizontalSquared)).Any(c =>
                            {
                                enterSpot = c.GetThingList(destMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                IntVec3 basePos;
                                IntVec3 faceCell;
                                if (enterSpot != null)
                                {
                                    basePos = enterSpot.Thing.PositionOnBaseMap();
                                    faceCell = enterSpot.Thing.BaseFullRotation().FacingCell;
                                }
                                else
                                {
                                    enterSpot = new TargetInfo(c, destMap);
                                    basePos = enterSpot.Cell.OrigToVehicleMap(vehicle);
                                    faceCell = enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle.VehicleMap).FacingCell;
                                }
                                faceCell.y = 0;
                                var dist = 1;
                                while ((basePos - faceCell * dist).GetThingList(destBaseMap).Contains(vehicle))
                                {
                                    dist++;
                                }
                                var cell = (basePos - faceCell * dist);
                                return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                                c.Standable(destMap) &&
                                destMap.reachability.CanReach(c, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest2 = result ? enterSpot : TargetInfo.Invalid;
                            return result;
                        }
                    }
                    //出発地と目的地がそれぞれ別の車上マップ
                    else
                    {
                        if (vehicle2 != null)
                        {
                            if (vehicle != null)
                            {
                                TargetInfo enterSpot = null;
                                TargetInfo enterSpot2 = null;
                                var result = vehicle2.InteractionCells.OrderBy(c => (c.OrigToVehicleMap(vehicle2) - dest3.Cell.OrigToVehicleMap(vehicle)).LengthHorizontalSquared)
                                    .Concat(CellRect.WholeMap(vehicle2.VehicleMap).EdgeCells.OrderBy(c => (c.OrigToVehicleMap(vehicle2) - dest3.Cell.OrigToVehicleMap(vehicle)).LengthHorizontalSquared)).Any(c =>
                                {
                                    enterSpot = c.GetThingList(departMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                    IntVec3 basePos;
                                    IntVec3 faceCell;
                                    if (enterSpot != null)
                                    {
                                        basePos = enterSpot.Thing.PositionOnBaseMap();
                                        faceCell = enterSpot.Thing.BaseFullRotation().FacingCell;
                                    }
                                    else
                                    {
                                        enterSpot = new TargetInfo(c, vehicle2.VehicleMap);
                                        basePos = enterSpot.Cell.OrigToVehicleMap(vehicle2);
                                        faceCell = enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle2.VehicleMap).FacingCell;
                                    }
                                    faceCell.y = 0;
                                    var dist = 1;
                                    while ((basePos - faceCell * dist).GetThingList(departBaseMap).Contains(vehicle2))
                                    {
                                        dist++;
                                    }
                                    var cell = (basePos - faceCell * dist);
                                    return vehicle.InteractionCells.OrderBy(c2 => (cell - c2.OrigToVehicleMap(vehicle)).LengthHorizontalSquared)
                                    .Concat(CellRect.WholeMap(vehicle.VehicleMap).EdgeCells.OrderBy(c2 => (cell - c2.OrigToVehicleMap(vehicle)).LengthHorizontalSquared)).Any(c2 =>
                                    {
                                        enterSpot2 = c2.GetThingList(destMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                        IntVec3 basePos2;
                                        IntVec3 faceCell2;
                                        if (enterSpot2 != null)
                                        {
                                            basePos2 = enterSpot2.Thing.PositionOnBaseMap();
                                            faceCell2 = enterSpot2.Thing.BaseFullRotation().FacingCell;
                                        }
                                        else
                                        {
                                            enterSpot2 = new TargetInfo(c2, vehicle.VehicleMap);
                                            basePos2 = enterSpot2.Cell.OrigToVehicleMap(vehicle);
                                            faceCell2 = enterSpot2.Cell.BaseFullDirectionToInsideMap(vehicle.VehicleMap).FacingCell;
                                        }
                                        faceCell2.y = 0;
                                        var dist2 = 1;
                                        while ((basePos2 - faceCell2 * dist2).GetThingList(destBaseMap).Contains(vehicle))
                                        {
                                            dist2++;
                                        }
                                        var cell2 = (basePos2 - faceCell2 * dist2);
                                        return cell.Standable(departBaseMap) &&
                                        c2.Standable(destMap) &&
                                        departMap.reachability.CanReach(root, enterSpot.Cell, PathEndMode.OnCell, traverseParms) &&
                                        departBaseMap.reachability.CanReach(cell, cell2, PathEndMode.OnCell, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger) &&
                                        destMap.reachability.CanReach(c2, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                                    });
                                });
                                dest1 = result ? enterSpot : TargetInfo.Invalid;
                                dest2 = result ? enterSpot2 : TargetInfo.Invalid;
                                return result;
                            }
                        }
                    }
                }
            }
            return false;
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
        public static bool CanReach(this Pawn pawn, LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode)
        {
            var traverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors, false, canBashFences);
            var destMap = dest3.HasThing ? dest3.Thing.MapHeld : pawn.BaseMap();
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
            var departMap = ReachabilityUtilityOnVehicle.tmpDepartMap;
            var destMap = dest.HasThing ? dest.Thing.MapHeld : map(reachability);
            var result = ReachabilityUtilityOnVehicle.CanReach(departMap, start, dest, peMode, traverseParms, destMap, out _, out _);
            return result;
        }

        public static Map tmpDepartMap;

        private static AccessTools.FieldRef<Reachability, Map> map = AccessTools.FieldRefAccess<Reachability, Map>("map");

        public static IntVec3 StandableCellNear(IntVec3 root, Map map, float radius, Predicate<IntVec3> validator, out Map destMap)
        {
            Map baseMap = map.BaseMap();
            if (root.TryGetFirstThing<VehiclePawnWithMap>(baseMap, out var vehicle))
            {
                var cell = root.VehicleMapToOrig(vehicle);
                if (cell.InBounds(vehicle.VehicleMap))
                {
                    destMap = vehicle.VehicleMap;
                    return CellFinder.StandableCellNear(cell, destMap, radius, validator);
                }
            }
            destMap = baseMap;
            return CellFinder.StandableCellNear(root, baseMap, radius, validator);
        }

        public static IntVec3 BestOrderedGotoDestNear(IntVec3 root, Pawn searcher, Predicate<IntVec3> cellValidator, Map map)
        {
            if (ReachabilityUtilityOnVehicle.IsGoodDest(root, searcher, cellValidator, map))
            {
                return root;
            }
            int num = 1;
            IntVec3 result = default(IntVec3);
            float num2 = -1000f;
            bool flag = false;
            int num3 = GenRadial.NumCellsInRadius(30f);
            do
            {
                IntVec3 intVec = root + GenRadial.RadialPattern[num];
                if (ReachabilityUtilityOnVehicle.IsGoodDest(intVec, searcher, cellValidator, map))
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

        public static bool IsGoodDest(IntVec3 c, Pawn searcher, Predicate<IntVec3> cellValidator, Map map)
		{
			if (cellValidator != null && !cellValidator(c))
			{
				return false;
			}

            if (!map.pawnDestinationReservationManager.CanReserve(c, searcher, true) || !searcher.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out _, out _))
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
                TargetInfo exitSpot = null;
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
            v_exitSpot = null;
            return false;
            Block_10:
            spot = intVec2;
            return true;
        }

        public static bool CanReachMapEdge(this Pawn carrier, Pawn pawn, out TargetInfo exitSpot)
        {
            exitSpot = null;
            if (!carrier.Spawned)
            {
                return false;
            }
            var traverseParms = TraverseParms.For(carrier);
            if (pawn.IsOnVehicleMapOf(out var vehicle))
            {
                var baseMap = pawn.BaseMap();
                Thing exitSpot2 = null;
                if(vehicle.InteractionCells.Any(c =>
                {
                    exitSpot2 = c.GetFirstThingWithComp<CompVehicleEnterSpot>(vehicle.VehicleMap);
                    if (exitSpot2 == null) return false;
                    return pawn.Map.reachability.CanReach(pawn.Position , exitSpot2, PathEndMode.OnCell, traverseParms) &&
                    baseMap.reachability.CanReachMapEdge(exitSpot2.PositionOnBaseMap() - exitSpot2.BaseFullRotation().FacingCell, traverseParms);
                }))
                {
                    exitSpot = exitSpot2;
                    return true;
                }
                return false;
            }
            return pawn.Map.reachability.CanReachMapEdge(pawn.Position, traverseParms);
        }

        public static bool CanReachVehicle(this VehiclePawn vehicle, LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, TraverseMode mode, Map destMap, out TargetInfo dest1, out TargetInfo dest2)
        {
            dest1 = TargetInfo.Invalid;
            dest2 = TargetInfo.Invalid;
            if (dest3.Cell == vehicle.Position && destMap == vehicle.Map)
            {
                return true;
            }
            var traverseParms = TraverseParms.For(vehicle, maxDanger, mode, false, false, false);
            if (!vehicle.Spawned) return false;

            var departMap = vehicle.Map;
            if (departMap == null || destMap == null) return false;
            var destBaseMap = destMap.IsVehicleMapOf(out var vehicle2) ? vehicle2.Map : destMap;
            var departBaseMap = departMap.IsVehicleMapOf(out var vehicle3) ? vehicle3.Map : departMap;

            if (departBaseMap == destBaseMap)
            {
                if (departMap == destMap)
                {
                    return MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, dest3, peMode, traverseParms);
                }
                else if (vehicle is VehiclePawnWithMap)
                {
                    return false;
                }
                else
                {
                    var flag = departMap == departBaseMap;
                    var flag2 = departBaseMap == destMap;
                    bool AvailableEnterSpot(Thing t){
                        return t.TryGetComp<CompVehicleEnterSpot>(out var comp) && comp.Props.allowPassingVehicle && t.def.size.x >= vehicle.VehicleDef.size.x;
                    }

                    //vehicleが車上マップに居て目的地がベースマップ
                    if (!flag && flag2)
                    {
                        if (vehicle3 != null)
                        {
                            Thing enterSpot = null;
                            var result = vehicle3.InteractionCells.OrderBy(c => (c.OrigToVehicleMap(vehicle3) - dest3.Cell).LengthHorizontalSquared).Any(c =>
                            {
                                enterSpot = c.GetThingList(departMap).FirstOrDefault(t => AvailableEnterSpot(t));
                                if (enterSpot == null) return false;
                                var basePos = enterSpot.PositionOnBaseMap();
                                var rot = enterSpot.BaseFullRotation();
                                var faceCell = rot.FacingCell;
                                faceCell.y = 0;
                                var dist = 1;
                                while ((basePos - faceCell * dist).GetThingList(departBaseMap).Contains(vehicle3))
                                {
                                    dist++;
                                }
                                var cell = basePos - faceCell * dist - faceCell * vehicle.HalfLength();
                                return MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, enterSpot, PathEndMode.OnCell, traverseParms) &&
                                vehicle.VehicleDef.CellRectStandable(departBaseMap, cell, rot.Opposite) &&
                                MapComponentCache<VehicleMapping>.GetComponent(departBaseMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest1 = result ? enterSpot : TargetInfo.Invalid;
                            return result;
                        }
                    }
                    //vehicleがベースマップに居て目的地が車上マップ
                    else if (flag && !flag2)
                    {
                        if (vehicle2 != null)
                        {
                            Thing enterSpot = null;
                            var result = vehicle2.InteractionCells.OrderBy(c => (vehicle.Position - c.OrigToVehicleMap(vehicle2)).LengthHorizontalSquared).Any(c =>
                            {
                                enterSpot = c.GetThingList(destMap).FirstOrDefault(t => AvailableEnterSpot(t));
                                if (enterSpot == null) return false;
                                var basePos = enterSpot.PositionOnBaseMap();
                                var rot = enterSpot.BaseFullRotation();
                                var faceCell = rot.FacingCell;
                                faceCell.y = 0;
                                var dist = 1;
                                while ((basePos - faceCell * dist).GetThingList(destBaseMap).Contains(vehicle2))
                                {
                                    dist++;
                                }
                                var cell = basePos - faceCell * dist - faceCell * vehicle.HalfLength();
                                var cell2 = enterSpot.Position + enterSpot.Rotation.FacingCell * vehicle.HalfLength();
                                return MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                                vehicle.VehicleDef.CellRectStandable(destMap, cell2, enterSpot.Rotation) &&
                                MapComponentCache<VehicleMapping>.GetComponent(destMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest2 = result ? enterSpot : TargetInfo.Invalid;
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
                                Thing enterSpot = null;
                                Thing enterSpot2 = null;
                                var result = vehicle3.InteractionCells.OrderBy(c => (c.OrigToVehicleMap(vehicle3) - dest3.Cell.OrigToVehicleMap(vehicle2)).LengthHorizontalSquared).Any(c =>
                                {
                                    enterSpot = c.GetThingList(departMap).FirstOrDefault(t => AvailableEnterSpot(t));
                                    if (enterSpot == null) return false;
                                    var basePos = enterSpot.PositionOnBaseMap();
                                    var rot = enterSpot.BaseFullRotation();
                                    var faceCell = rot.FacingCell;
                                    faceCell.y = 0;
                                    var dist = 1;
                                    while ((basePos - faceCell * dist).GetThingList(departBaseMap).Contains(vehicle3))
                                    {
                                        dist++;
                                    }
                                    var cell = basePos - faceCell * dist - faceCell * vehicle.HalfLength();
                                    return vehicle2.InteractionCells.OrderBy(c2 => (cell - c2.OrigToVehicleMap(vehicle2)).LengthHorizontalSquared).Any(c2 =>
                                    {
                                        enterSpot2 = c2.GetThingList(destMap).FirstOrDefault(t => AvailableEnterSpot(t));
                                        if (enterSpot2 == null) return false;
                                        var basePos2 = enterSpot2.PositionOnBaseMap();
                                        var rot2 = enterSpot2.BaseFullRotation();
                                        var faceCell2 = rot2.FacingCell;
                                        faceCell2.y = 0;
                                        var dist2 = 1;
                                        while ((basePos2 - faceCell2 * dist2).GetThingList(destBaseMap).Contains(vehicle2))
                                        {
                                            dist2++;
                                        }
                                        var cell2 = basePos2 - faceCell2 * dist2 - faceCell2 * vehicle.HalfLength();
                                        var cell3 = enterSpot2.Position + enterSpot.Rotation.FacingCell * vehicle.HalfLength();
                                        return MapComponentCache<VehicleMapping>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, enterSpot, PathEndMode.OnCell, traverseParms) &&
                                        vehicle.VehicleDef.CellRectStandable(departBaseMap, cell, rot.Opposite) &&
                                        MapComponentCache<VehicleMapping>.GetComponent(departBaseMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, cell2, PathEndMode.OnCell, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger) &&
                                        vehicle.VehicleDef.CellRectStandable(destMap, cell3, enterSpot2.Rotation) &&
                                        MapComponentCache<VehicleMapping>.GetComponent(destMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell3, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                                    });
                                });
                                dest1 = result ? enterSpot : TargetInfo.Invalid;
                                dest2 = result ? enterSpot2 : TargetInfo.Invalid;
                                return result;
                            }
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
