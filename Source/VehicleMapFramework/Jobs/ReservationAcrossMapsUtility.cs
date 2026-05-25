using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class ReservationAcrossMapsUtility
{
  private static readonly List<ReservationManager.Reservation> tmpReservations = [];

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

  //public static bool HasReserved<TDriver>(this Pawn p, LocalTargetInfo target, Map destMap, LocalTargetInfo? targetAIsNot = null, LocalTargetInfo? targetBIsNot = null, LocalTargetInfo? targetCIsNot = null)
  //{
  //    return p.Spawned && destMap.reservationManager.ReservedBy<TDriver>(target, p, targetAIsNot, targetBIsNot, targetCIsNot);
  //}

  extension(Pawn p)
  {
    public bool CanReserve(LocalTargetInfo target, int maxPawns, int stackCount, ReservationLayerDef layer, bool ignoreOtherReservations, Map map)
    {
      if (p == null)
      {
        Log.Error("CanReserve with null claimant");
        return false;
      }
      if (!p.Spawned || p.BaseMapOrCaravan != map.BaseMapOrCaravan)
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
      var num = target.HasThing ? target.Thing.stackCount : 1;
      var num2 = stackCount == -1 ? num : stackCount;
      if (num2 > num)
      {
        return false;
      }

      if (ignoreOtherReservations) return true;
      if (map.physicalInteractionReservationManager.IsReserved(target) && !map.physicalInteractionReservationManager.IsReservedBy(p, target))
      {
        return false;
      }
      if (MultiFloors.Active && map != p.Map)
      {
        if (p.Map.physicalInteractionReservationManager.IsReserved(target) && !p.Map.physicalInteractionReservationManager.IsReservedBy(p, target))
        {
          return false;
        }
      }

      tmpReservations.Clear();
      tmpReservations.AddRange(map.reservationManager.ReservationsReadOnly);
      if (MultiFloors.Active && map != p.Map)
      {
        tmpReservations.AddRange(p.Map.reservationManager.ReservationsReadOnly);
      }
      if (tmpReservations.Any(reservation =>
            reservation.Target == target && reservation.Layer == layer && reservation.Claimant == p &&
            (reservation.StackCount == -1 || reservation.StackCount >= num2)))
      {
        return true;
      }
      if (target is { HasThing: true, Thing: Building building } && building.def.hasInteractionCell)
      {
        var interactionCell = building.InteractionCell;
        var edifice = interactionCell.GetEdifice(map);
        if (edifice != null)
        {
          if (map.reservationManager.TryGetReserver(edifice, p.Faction, out var pawn) && pawn.Spawned && pawn != p)
          {
            return false;
          }
        }
        else if (map.reservationManager.TryGetReserver(interactionCell, p.Faction, out var pawn2) && pawn2.Spawned && pawn2 != p)
        {
          return false;
        }
      }
      var num3 = 0;
      var num4 = 0;
      foreach (var reservation in tmpReservations.Where(reservation =>
                 reservation.Target == target && reservation.Layer == layer && reservation.Claimant != p &&
                 RespectsReservationsOf(p, reservation.Claimant)))
      {
        if (reservation.MaxPawns != maxPawns)
        {
          return false;
        }
        num3++;
        if (reservation.StackCount == -1)
        {
          num4 += num;
        }
        else
        {
          num4 += reservation.StackCount;
        }
        if (num3 >= maxPawns || num2 + num4 > num)
        {
          return false;
        }
      }
      return true;
    }

    public bool CanReserveNew(LocalTargetInfo target, Map destMap)
    {
      return target.IsValid && !p.HasReserved(target, null, destMap) && p.CanReserve(target, 1, -1, null, false, destMap);
    }

    public bool HasReserved(LocalTargetInfo target, Job job, Map destMap)
    {
      return p.Spawned && destMap.reservationManager.ReservedBy(target, p, job);
    }

    public bool Reserve(Map map, LocalTargetInfo target, Job job, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool errorOnFailed = true, bool ignoreOtherReservations = false)
    {
      if (map == null && target.HasThing)
      {
        map = target.Thing.MapHeld;
      }
      return map != null && map.reservationManager.Reserve(p, job, target, maxPawns, stackCount, layer, errorOnFailed, ignoreOtherReservations);
    }
  }

  //public static void ReserveAsManyAsPossible(this Pawn p, Map map, List<LocalTargetInfo> target, Job job, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null)
  //{
  //    if (!p.Spawned)
  //    {
  //        return;
  //    }
  //    for (int i = 0; i < target.Count; i++)
  //    {
  //        var destMap = target[i].Thing?.MapHeld ?? map ?? p.MapHeld;
  //        destMap.reservationManager.Reserve(p, job, target[i], maxPawns, stackCount, layer, false, false, false);
  //    }
  //}

  //public static bool ReserveSittableOrSpot(this Pawn pawn, Map map, IntVec3 exactSittingPos, Job job, bool errorOnFailed = true)
  //{
  //    Building edifice = exactSittingPos.GetEdifice(map);
  //    if (exactSittingPos.Impassable(map))
  //    {
  //        Log.Error("Tried reserving impassable sittable or spot.");
  //        return false;
  //    }
  //    if (edifice == null || edifice.def.building.multiSittable)
  //    {
  //        return pawn.Reserve(map, exactSittingPos, job, 1, -1, null, errorOnFailed, false);
  //    }
  //    return (edifice == null || !edifice.def.building.isSittable || !edifice.def.hasInteractionCell || !(exactSittingPos != edifice.InteractionCell)) && pawn.Reserve(map, edifice, job, 1, -1, null, errorOnFailed, false);
  //}

  //public static bool CanReserveAndReach(this Pawn p, Map targMap, LocalTargetInfo target, PathEndMode peMode, Danger maxDanger, int maxPawns, int stackCount, ReservationLayerDef layer, bool ignoreOtherReservations, out TargetInfo exitSpot, out TargetInfo enterSpot)
  //{
  //    exitSpot = TargetInfo.Invalid;
  //    enterSpot = TargetInfo.Invalid;
  //    return p.Spawned && p.CanReach(target, peMode, maxDanger, false, false, TraverseMode.ByPawn, targMap, out exitSpot, out enterSpot) &&
  //        p.CanReserve(target, maxPawns, stackCount, layer, ignoreOtherReservations, targMap);
  //}

  //public static bool CanReserveSittableOrSpot_NewTemp(this Pawn pawn, Map map, IntVec3 exactSittingPos, Thing ignoreThing, bool ignoreOtherReservations = false)
  //{
  //    Building edifice = exactSittingPos.GetEdifice(map);
  //    if (exactSittingPos.Impassable(map) || exactSittingPos.IsForbidden(pawn))
  //    {
  //        return false;
  //    }

  //    for (int i = 0; i < 4; i++)
  //    {
  //        IntVec3 c = exactSittingPos + GenAdj.CardinalDirections[i];
  //        if (c.InBounds(map))
  //        {
  //            Building edifice2 = c.GetEdifice(map);
  //            if (edifice2 != null && edifice2 != ignoreThing && edifice2.def.hasInteractionCell && edifice2.InteractionCell == exactSittingPos && map.reservationManager.TryGetReserver(edifice2, pawn.Faction, out var reserver) && reserver.Spawned && reserver != pawn)
  //            {
  //                return false;
  //            }
  //        }
  //    }

  //    if (edifice == null || edifice.def.building.multiSittable)
  //    {
  //        return pawn.CanReserve(exactSittingPos, 1, -1, null, ignoreOtherReservations, map);
  //    }

  //    if (edifice.def.building.isSittable && edifice.def.hasInteractionCell && exactSittingPos != edifice.InteractionCell)
  //    {
  //        return false;
  //    }

  //    return pawn.CanReserve(edifice, 1, -1, null, ignoreOtherReservations, map);
  //}
}
