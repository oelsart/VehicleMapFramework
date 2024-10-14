using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;

namespace VehicleInteriors
{
    public static class ReachabilityUtilityOnVehicle
    {
        public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest3, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out LocalTargetInfo dest1, out LocalTargetInfo dest2)
        {
            MapParent_Vehicle parentVehicle;
            MapParent_Vehicle parentVehicle2;
            var destBaseMap = (parentVehicle = destMap.Parent as MapParent_Vehicle) != null ? parentVehicle.vehicle.Map : destMap;
            var departBaseMap = (parentVehicle2 = departMap.Parent as MapParent_Vehicle) != null ? parentVehicle2.vehicle.Map : departMap;
            dest1 = LocalTargetInfo.Invalid;
            dest2 = LocalTargetInfo.Invalid;

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
                        if (parentVehicle2 != null)
                        {
                            Thing enterSpot = null;
                            var result = parentVehicle2.vehicle.InteractionCells.Any(c =>
                            {
                                enterSpot = c.GetThingList(departMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                if (enterSpot == null) return false;
                                var cell = (c - enterSpot.Rotation.FacingCell).OrigToVehicleMap(parentVehicle2.vehicle);
                                return departMap.reachability.CanReach(root, enterSpot, PathEndMode.OnCell, traverseParms) &&
                                departBaseMap.reachability.CanReach(cell, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest1 = enterSpot;
                            return result;
                        }
                    }
                    else if (flag && !flag2)
                    {
                        if (parentVehicle != null)
                        {
                            Thing enterSpot = null;
                            var result = parentVehicle.vehicle.InteractionCells.Any(c =>
                            {
                                enterSpot = c.GetThingList(destMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                if (enterSpot == null) return false;
                                var cell = (c - enterSpot.Rotation.FacingCell).OrigToVehicleMap(parentVehicle.vehicle);
                                return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                                destMap.reachability.CanReach(c, dest3, peMode, TraverseMode.PassAllDestroyableThings, traverseParms.maxDanger);
                            });
                            dest2 = enterSpot;
                            return result;
                        }
                    }
                    else
                    {
                        if (parentVehicle2 != null)
                        {
                            if (parentVehicle != null)
                            {
                                Thing enterSpot = null;
                                Thing enterSpot2 = null;
                                var result = parentVehicle2.vehicle.InteractionCells.Any(c =>
                                {
                                    enterSpot = c.GetThingList(departMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                    if (enterSpot == null) return false;
                                    var cell = (c - enterSpot.Rotation.FacingCell).OrigToVehicleMap(parentVehicle2.vehicle);
                                    return parentVehicle.vehicle.InteractionCells.Any(c2 =>
                                    {
                                        enterSpot2 = c2.GetThingList(destMap).FirstOrDefault(t => t.HasComp<CompVehicleEnterSpot>());
                                        if (enterSpot2 == null) return false;
                                        var cell2 = (c2 - enterSpot2.Rotation.FacingCell).OrigToVehicleMap(parentVehicle.vehicle);
                                        return departMap.reachability.CanReach(root, enterSpot, PathEndMode.OnCell, traverseParms) &&
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

        public static bool CanReach(Map departMap, LocalTargetInfo dest3, PathEndMode peMode, TraverseParms traverseParms, Map destMap, out LocalTargetInfo dest1, out LocalTargetInfo dest2)
        {
            return ReachabilityUtilityOnVehicle.CanReach(departMap, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out dest1, out dest2);
        }

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
            if (root.TryGetFirstThing<VehiclePawnWithInterior>(baseMap, out var vehicle))
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

        internal static bool IsGoodDest(IntVec3 c, Pawn searcher, Predicate<IntVec3> cellValidator, Map map)
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
    }
}
