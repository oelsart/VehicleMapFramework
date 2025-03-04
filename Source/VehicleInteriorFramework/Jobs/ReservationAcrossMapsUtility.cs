using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
            if (!claimant.Spawned || claimant.BaseMap() != map.BaseMap())
            {
                return false;
            }
            if (!target.IsValid || target.ThingDestroyed)
            {
                return false;
            }
            if (target.HasThing && target.Thing.SpawnedOrAnyParentSpawned && target.Thing.MapHeld != map)
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
                if (target.HasThing && target.Thing is Building building && building.def.hasInteractionCell)
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
            return target.IsValid && !p.HasReserved(target, null, destMap) && p.CanReserve(target, destMap, 1, -1, null, false);
        }

        public static bool HasReserved(this Pawn p, LocalTargetInfo target, Job job, Map destMap)
        {
            if (!p.Spawned)
            {
                return false;
            }

            return destMap.reservationManager.ReservedBy(target, p, job);
        }

        public static bool Reserve(this Pawn p, Map map, LocalTargetInfo target, Job job, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool errorOnFailed = true, bool ignoreOtherReservations = false)
        {
            if (map == null && target.HasThing)
            {
                map = target.Thing.MapHeld;
            }
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
                var destMap = target[i].Thing?.MapHeld ?? map ?? p.MapHeld;
                destMap.reservationManager.Reserve(p, job, target[i], maxPawns, stackCount, layer, false, false, false);
            }
        }

        public static bool ReserveSittableOrSpot(this Pawn pawn, Map map, IntVec3 exactSittingPos, Job job, bool errorOnFailed = true)
        {
            Building edifice = exactSittingPos.GetEdifice(map);
            if (exactSittingPos.Impassable(map))
            {
                Log.Error("Tried reserving impassable sittable or spot.");
                return false;
            }
            if (edifice == null || edifice.def.building.multiSittable)
            {
                return pawn.Reserve(map, exactSittingPos, job, 1, -1, null, errorOnFailed, false);
            }
            return (edifice == null || !edifice.def.building.isSittable || !edifice.def.hasInteractionCell || !(exactSittingPos != edifice.InteractionCell)) && pawn.Reserve(map, edifice, job, 1, -1, null, errorOnFailed, false);
        }

        public static bool CanReserveAndReach(this Pawn p, Map targMap, LocalTargetInfo target, PathEndMode peMode, Danger maxDanger, int maxPawns, int stackCount, ReservationLayerDef layer, bool ignoreOtherReservations, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            return p.Spawned && p.CanReach(target, peMode, maxDanger, false, false, TraverseMode.ByPawn, targMap, out exitSpot, out enterSpot) &&
                p.CanReserve(target, targMap, maxPawns, stackCount, layer, ignoreOtherReservations);
        }

        public static bool IsForbidden(this Thing t, Pawn pawn)
        {
            if (!ForbidUtility.CaresAboutForbidden(pawn, false, false))
            {
                return false;
            }
            if ((t.Spawned || t.SpawnedParentOrMe != pawn) && t.PositionHeldOnBaseMap().IsForbidden(pawn))
            {
                return true;
            }
            if (t.IsForbidden(pawn.Faction) || t.IsForbidden(pawn.HostFaction))
            {
                return true;
            }
            Lord lord = pawn.GetLord();
            if (lord != null && lord.extraForbiddenThings.Contains(t))
            {
                return true;
            }
            foreach (Lord lord2 in pawn.MapHeld.lordManager.lords)
            {
                if (lord2.CurLordToil is LordToil_Ritual lordToil_Ritual && lordToil_Ritual.ReservedThings.Contains(t) && lord2 != lord)
                {
                    return true;
                }
                LordToil_PsychicRitual lordToil_PsychicRitual;
                PsychicRitualDef_InvocationCircle psychicRitualDef_InvocationCircle;
                if ((lordToil_PsychicRitual = (lord2.CurLordToil as LordToil_PsychicRitual)) != null && (psychicRitualDef_InvocationCircle = (lordToil_PsychicRitual.RitualData.psychicRitual.def as PsychicRitualDef_InvocationCircle)) != null && psychicRitualDef_InvocationCircle.TargetRole != null && lordToil_PsychicRitual.RitualData.psychicRitual.assignments.FirstAssignedPawn(psychicRitualDef_InvocationCircle.TargetRole) == t && !(lordToil_PsychicRitual.RitualData.CurPsychicRitualToil is PsychicRitualToil_TargetCleanup) && lord2 != lord)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CanReserveSittableOrSpot_NewTemp(this Pawn pawn, Map map, IntVec3 exactSittingPos, Thing ignoreThing, bool ignoreOtherReservations = false)
        {
            Building edifice = exactSittingPos.GetEdifice(map);
            if (exactSittingPos.Impassable(map) || exactSittingPos.IsForbidden(pawn))
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                IntVec3 c = exactSittingPos + GenAdj.CardinalDirections[i];
                if (c.InBounds(map))
                {
                    Building edifice2 = c.GetEdifice(map);
                    if (edifice2 != null && edifice2 != ignoreThing && edifice2.def.hasInteractionCell && edifice2.InteractionCell == exactSittingPos && map.reservationManager.TryGetReserver(edifice2, pawn.Faction, out var reserver) && reserver.Spawned && reserver != pawn)
                    {
                        return false;
                    }
                }
            }

            if (edifice == null || edifice.def.building.multiSittable)
            {
                return pawn.CanReserve(exactSittingPos, map, 1, -1, null, ignoreOtherReservations);
            }

            if (edifice.def.building.isSittable && edifice.def.hasInteractionCell && exactSittingPos != edifice.InteractionCell)
            {
                return false;
            }

            return pawn.CanReserve(edifice, map, 1, -1, null, ignoreOtherReservations);
        }
    }
}
