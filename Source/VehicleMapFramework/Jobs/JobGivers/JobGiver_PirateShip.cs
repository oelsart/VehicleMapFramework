using RimWorld;
using UnityEngine.Assertions;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobGiver_PirateShip : JobGiver_CombatFormation
{
    protected override IntRange ExpiryInterval => new (360);

    protected override bool TryFindCombatPosition(VehiclePawn vehicle, out IntVec3 dest)
    {
        return CombatPositionUtility.TryFindShipCombatPosition(vehicle, out dest, out _);
    }
    
    protected override void UpdateEnemyTarget(VehiclePawn vehicle)
    {
        var thing = vehicle.mindState.enemyTarget;
        if (thing != null && ShouldLoseTarget(vehicle))
        {
            thing = null;
        }
        if (thing == null)
        {
            var minDist = float.MaxValue;
            var potentialTargetsFor =
                vehicle.Map.attackTargetsCache.GetPotentialTargetsFor(vehicle);
            for (var i = 0; i < potentialTargetsFor.Count; i++)
            {
                var attackTarget = potentialTargetsFor[i];
                // Threat Disabled
                if (attackTarget.ThreatDisabled(vehicle)) continue;
                // Dormant / Non-targetable
                if (!AttackTargetFinder.IsAutoTargetable(attackTarget)) continue;
                // Humanlikes-only
                if (humanlikesOnly && attackTarget.Thing is Pawn targetPawn &&
                    !targetPawn.RaceProps.Humanlike) continue;
                // No line of sight
                //if (attackTarget.Thing is Pawn innerTargetPawn && (innerTargetPawn.IsCombatant() || !ignoreNonCombatants) 
                //	&& !GenSight.LineOfSightToThing(vehicle.Position, innerTargetPawn, vehicle.Map, false, null)) continue;

                thing = (Thing)attackTarget;
                var dist = thing.PositionOnBaseMap.DistanceToSquared(vehicle.PositionOnBaseMap);
                if (dist < minDist && vehicle.CanReachVehicle(thing.Position, PathEndMode.Touch,
                        Danger.Deadly, TraverseMode.ByPawn, thing.Map, out _, out _))
                {
                    minDist = dist;
                }
            }
        }
        vehicle.mindState.enemyTarget = thing;
        if (thing is Pawn && thing.Faction == Faction.OfPlayer &&
            vehicle.PositionOnBaseMap.InHorDistOf(thing.PositionOnBaseMap, 60f))
        {
            Find.TickManager.slower.SignalForceNormalSpeed();
        }
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        var vehicle = pawn as VehiclePawn;
        Assert.IsNotNull(vehicle, "Trying to assign vehicle job to non-vehicle pawn.");

        UpdateEnemyTarget(vehicle);
        if (vehicle!.mindState.enemyTarget is not { } enemyTarget)
        {
            return null;
        }
        if (enemyTarget is Pawn targetPawn && targetPawn.IsPsychologicallyInvisible())
        {
            return null;
        }
        if (OnlyUseRanged)
        {
            if (!TryFindCombatPosition(vehicle, out var cell))
            {
                return null;
            }

            var job = cell == vehicle.Position
                ? JobMaker.MakeJob(JobDefOf_Vehicles.IdleVehicle, vehicle)
                : JobMaker.MakeJob(VMF_DefOf.VMF_GotoShipCombat, cell);
            job.expiryInterval = ExpiryInterval.RandomInRange;
            job.checkOverrideOnExpire = true;
            return job;
        }
        return null;
    }
}