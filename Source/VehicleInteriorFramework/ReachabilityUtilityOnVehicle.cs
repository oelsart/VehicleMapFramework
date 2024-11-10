using RimWorld;
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
        public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest3, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out LocalTargetInfo dest1, out LocalTargetInfo dest2)
        {
            dest1 = LocalTargetInfo.Invalid;
            dest2 = LocalTargetInfo.Invalid;
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

                    if (!flag && flag2)
                    {
                        if (vehicle2 != null)
                        {
                            Thing enterSpot = null;
                            var result = vehicle2.InteractionCells.Any(c =>
                            {
                                enterSpot = c.GetThingList(departMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                if (enterSpot == null) return false;
                                var basePos = enterSpot.PositionOnBaseMap();
                                var faceCell = enterSpot.BaseFullRotationOfThing().FacingCell;
                                faceCell.y = 0;
                                var dist = 1;
                                while ((basePos - faceCell * dist).GetThingList(departBaseMap).Contains(vehicle2))
                                {
                                    dist++;
                                }
                                var cell = (basePos - faceCell * dist);
                                return departMap.reachability.CanReach(root, enterSpot, PathEndMode.OnCell, traverseParms) &&
                                cell.Walkable(departBaseMap) &&
                                departBaseMap.reachability.CanReach(cell, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest1 = enterSpot;
                            return result;
                        }
                    }
                    else if (flag && !flag2)
                    {
                        if (vehicle != null)
                        {
                            Thing enterSpot = null;
                            var result = vehicle.InteractionCells.Any(c =>
                            {
                                enterSpot = c.GetThingList(destMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                if (enterSpot == null) return false;
                                var basePos = enterSpot.PositionOnBaseMap();
                                var faceCell = enterSpot.BaseFullRotationOfThing().FacingCell;
                                faceCell.y = 0;
                                var dist = 1;
                                while ((basePos - faceCell * dist).GetThingList(destBaseMap).Contains(vehicle))
                                {
                                    dist++;
                                }
                                var cell = (basePos - faceCell * dist);
                                return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                                destMap.reachability.CanReach(c, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest2 = enterSpot;
                            return result;
                        }
                    }
                    else
                    {
                        if (vehicle2 != null)
                        {
                            if (vehicle != null)
                            {
                                Thing enterSpot = null;
                                Thing enterSpot2 = null;
                                var result = vehicle2.InteractionCells.Any(c =>
                                {
                                    enterSpot = c.GetThingList(departMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                    if (enterSpot == null) return false;
                                    var basePos = enterSpot.PositionOnBaseMap();
                                    var faceCell = enterSpot.BaseFullRotationOfThing().FacingCell;
                                    faceCell.y = 0;
                                    var dist = 1;
                                    while ((basePos - faceCell * dist).GetThingList(departBaseMap).Contains(vehicle2))
                                    {
                                        dist++;
                                    }
                                    var cell = (basePos - faceCell * dist);
                                    return vehicle.InteractionCells.Any(c2 =>
                                    {
                                        enterSpot2 = c2.GetThingList(destMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                        if (enterSpot2 == null) return false;
                                        var basePos2 = enterSpot2.PositionOnBaseMap();
                                        var faceCell2 = enterSpot2.BaseFullRotationOfThing().FacingCell;
                                        faceCell2.y = 0;
                                        var dist2 = 1;
                                        while ((basePos2 - faceCell2 * dist2).GetThingList(destBaseMap).Contains(vehicle))
                                        {
                                            dist2++;
                                        }
                                        var cell2 = (basePos2 - faceCell2 * dist2);
                                        return departMap.reachability.CanReach(root, enterSpot, PathEndMode.OnCell, traverseParms) &&
                                        cell.Walkable(departBaseMap) &&
                                        departBaseMap.reachability.CanReach(cell, cell2, PathEndMode.OnCell, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger) &&
                                        destMap.reachability.CanReach(c2, dest3, PathEndMode.OnCell, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                                    });
                                });
                                dest1 = enterSpot;
                                dest2 = enterSpot2;
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

        public static bool CanReach(this Pawn pawn, LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode, Map destMap, out LocalTargetInfo dest1, out LocalTargetInfo dest2)
        {
            var traverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors, false, canBashFences);
            dest1 = LocalTargetInfo.Invalid;
            dest2 = LocalTargetInfo.Invalid;
            return pawn.Spawned && ReachabilityUtilityOnVehicle.CanReach(pawn.Map, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out dest1, out dest2);
        }

        public static IntVec3 StandableCellNear(IntVec3 root, Map map, float radius, Predicate<IntVec3> validator, out Map destMap)
        {
            Map baseMap = map.BaseMap();
            if (root.TryGetFirstThing<VehiclePawnWithMap>(baseMap, out var vehicle))
            {
                var cell = root.VehicleMapToOrig(vehicle);
                if (cell.InBounds(vehicle.interiorMap))
                {
                    destMap = vehicle.interiorMap;
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

        public static bool TryFindBestExitSpot(Pawn carrier, Pawn pawn, out IntVec3 spot, out LocalTargetInfo v_exitSpot, TraverseMode mode = TraverseMode.ByPawn, bool canBash = true)
        {
            var baseMap = pawn.BaseMap();
            if ((mode == TraverseMode.PassAllDestroyableThings || mode == TraverseMode.PassAllDestroyableThingsNotWater || mode == TraverseMode.PassAllDestroyablePlayerOwnedThings) && !carrier.CanReachMapEdge(pawn, out v_exitSpot))
            {
                LocalTargetInfo exitSpot = null;
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

        public static bool CanReachMapEdge(this Pawn carrier, Pawn pawn, out LocalTargetInfo exitSpot)
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
                    exitSpot2 = c.GetFirstThingWithComp<CompVehicleEnterSpot>(vehicle.interiorMap);
                    if (exitSpot2 == null) return false;
                    return pawn.Map.reachability.CanReach(pawn.Position , exitSpot2, PathEndMode.OnCell, traverseParms) &&
                    baseMap.reachability.CanReachMapEdge(exitSpot2.PositionOnBaseMap() - exitSpot2.BaseFullRotationOfThing().FacingCell, traverseParms);
                }))
                {
                    exitSpot = exitSpot2;
                    return true;
                }
                return false;
            }
            return pawn.Map.reachability.CanReachMapEdge(pawn.Position, traverseParms);
        }
    }
}
