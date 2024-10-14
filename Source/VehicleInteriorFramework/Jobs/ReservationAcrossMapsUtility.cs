﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;

namespace VehicleInteriors
{
    public static class ReservationAcrossMapsUtility
    {
        //Transpilerで元のCanReserveと置き換えるためにこうしている
        public static bool CanReserve(ReservationManager manager, Pawn claimant, LocalTargetInfo target, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool ignoreOtherReservations = false, Map map = null)
        {
            if (claimant == null)
            {
                Log.Error("CanReserve with null claimant");
                return false;
            }
            if (!claimant.Spawned || claimant.BaseMapOfThing() != map.BaseMap())
            {
                return false;
            }
            if (!target.IsValid || target.ThingDestroyed)
            {
                return false;
            }
            if (target.HasThing && target.Thing.SpawnedOrAnyParentSpawned && target.Thing.MapHeldBaseMap() != map.BaseMap())
            {
                return false;
            }
            int num = target.HasThing ? target.Thing.stackCount : 1;
            int num2 = (stackCount == -1) ? num : stackCount;
            if (num2 > num)
            {
                return false;
            }
            if (!ignoreOtherReservations)
            {
                var destMapReservations = map.reservationManager.ReservationsReadOnly;
                if (map.physicalInteractionReservationManager.IsReserved(target) && !map.physicalInteractionReservationManager.IsReservedBy(claimant, target))
                {
                    return false;
                }
                for (int i = 0; i < destMapReservations.Count; i++)
                {
                    ReservationManager.Reservation reservation = destMapReservations[i];
                    if (reservation.Target == target && reservation.Layer == layer && reservation.Claimant == claimant && (reservation.StackCount == -1 || reservation.StackCount >= num2))
                    {
                        return true;
                    }
                }
                Building building;
                if (target.HasThing && (building = (target.Thing as Building)) != null && building.def.hasInteractionCell)
                {
                    IntVec3 interactionCell = building.InteractionCell;
                    Building edifice = interactionCell.GetEdifice(map);
                    if (edifice != null)
                    {
                        if (map.reservationManager.TryGetReserver(edifice, claimant.Faction, out var pawn) && pawn.Spawned && pawn != claimant)
                        {
                            return false;
                        }
                    }
                    else if (map.reservationManager.TryGetReserver(interactionCell, claimant.Faction, out var pawn2) && pawn2.Spawned && pawn2 != claimant)
                    {
                        return false;
                    }
                }
                int num3 = 0;
                int num4 = 0;
                for (int j = 0; j < destMapReservations.Count; j++)
                {
                    ReservationManager.Reservation reservation2 = destMapReservations[j];
                    if (!(reservation2.Target != target) && reservation2.Layer == layer && reservation2.Claimant != claimant && ReservationAcrossMapsUtility.RespectsReservationsOf(claimant, reservation2.Claimant))
                    {
                        if (reservation2.MaxPawns != maxPawns)
                        {
                            return false;
                        }
                        num3++;
                        if (reservation2.StackCount == -1)
                        {
                            num4 += num;
                        }
                        else
                        {
                            num4 += reservation2.StackCount;
                        }
                        if (num3 >= maxPawns || num2 + num4 > num)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public static bool CanReserve(this Pawn claimant, LocalTargetInfo target, Map destMap, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool ignoreOtherReservations = false)
        {
            return ReservationAcrossMapsUtility.CanReserve(null, claimant, target, maxPawns, stackCount, layer, ignoreOtherReservations, destMap);
        }

        private static bool RespectsReservationsOf(Pawn newClaimant, Pawn oldClaimant)
        {
            if (newClaimant == oldClaimant)
            {
                return true;
            }
            if (newClaimant.Faction == null || oldClaimant.Faction == null)
            {
                return false;
            }
            if (newClaimant.Faction == oldClaimant.Faction)
            {
                return true;
            }
            if (!newClaimant.Faction.HostileTo(oldClaimant.Faction))
            {
                return true;
            }
            if (oldClaimant.HostFaction != null && oldClaimant.HostFaction == newClaimant.HostFaction)
            {
                return true;
            }
            if (newClaimant.HostFaction != null)
            {
                if (oldClaimant.HostFaction != null)
                {
                    return true;
                }
                if (newClaimant.HostFaction == oldClaimant.Faction)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasReserved<TDriver>(this Pawn p, LocalTargetInfo target, Map destMap, LocalTargetInfo? targetAIsNot = null, LocalTargetInfo? targetBIsNot = null, LocalTargetInfo? targetCIsNot = null)
        {
            return p.Spawned && destMap.reservationManager.ReservedBy<TDriver>(target, p, targetAIsNot, targetBIsNot, targetCIsNot);
        }

        public static bool CanReserveNew(this Pawn p, LocalTargetInfo target, Map destMap)
        {
            return target.IsValid && !p.HasReserved(target, null) && p.CanReserve(target, destMap, 1, -1, null, false);
        }

        public static bool Reserve(this Pawn p, Map map, LocalTargetInfo target, Job job, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool errorOnFailed = true, bool ignoreOtherReservations = false)
        {
            return map.reservationManager.Reserve(p, job, target, maxPawns, stackCount, layer, errorOnFailed, ignoreOtherReservations, true);
        }

        public static void ReserveAsManyAsPossible(this Pawn p, Map map, List<LocalTargetInfo> target, Job job, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null)
        {
            if (!p.Spawned)
            {
                return;
            }
            for (int i = 0; i < target.Count; i++)
            {
                var destMap = target[i].HasThing ? target[i].Thing.Map : map;
                destMap.reservationManager.Reserve(p, job, target[i], maxPawns, stackCount, layer, false, false, false);
            }
        }
    }
}