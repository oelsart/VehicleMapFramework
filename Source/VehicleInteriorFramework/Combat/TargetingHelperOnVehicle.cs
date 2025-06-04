﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class TargetingHelperOnVehicle
    {
        /// <summary>
        /// Best attack target for VehicleTurret
        /// </summary>
        public static IAttackTarget BestAttackTarget(VehicleTurret turret, TargetScanFlags flags, Predicate<Thing> validator = null, float minDist = 0f, float maxDist = 9999f, IntVec3 locus = default(IntVec3), float maxTravelRadiusFromLocus = 3.4028235E+38f, bool canBash = false, bool canTakeTargetsCloserThanEffectiveMinRange = true)
        {
            VehiclePawn searcherPawn = turret.vehicle;

            float minDistSquared = minDist * minDist;
            float num = maxTravelRadiusFromLocus + turret.MaxRange;
            float maxLocusDistSquared = num * num;
            var searcherPosOnBaseMap = searcherPawn.PositionOnBaseMap();
            var baseMap = searcherPawn.BaseMap();

            Func<IntVec3, bool> losValidator = null;
            if (flags.HasFlag(TargetScanFlags.LOSBlockableByGas))
            {
                losValidator = (pos) => pos.AnyGas(searcherPawn.BaseMap(), GasType.BlindSmoke);
            }
            bool innerValidator(IAttackTarget t)
            {
                Thing thing = t.Thing;
                var thingPosOnBaseMap = thing.PositionOnBaseMap();
                if (t == searcherPawn)
                {
                    return false;
                }
                if (minDistSquared > 0f && (searcherPosOnBaseMap - thingPosOnBaseMap).LengthHorizontalSquared < minDistSquared)
                {
                    return false;
                }
                if (!canTakeTargetsCloserThanEffectiveMinRange)
                {
                    float num2 = turret.MinRange;
                    if (num2 > 0f && (turret.vehicle.PositionOnBaseMap() - thingPosOnBaseMap).LengthHorizontalSquared < num2 * num2)
                    {
                        return false;
                    }
                }
                if (maxTravelRadiusFromLocus < 9999f && (thingPosOnBaseMap - locus).LengthHorizontalSquared > maxLocusDistSquared)
                {
                    return false;
                }
                if (!searcherPawn.HostileTo(thing))
                {
                    return false;
                }
                if (validator != null && !validator(thing))
                {
                    return false;
                }
                if ((flags & TargetScanFlags.NeedLOSToAll) != TargetScanFlags.None)
                {
                    if (losValidator != null && (!losValidator(searcherPosOnBaseMap) || !losValidator(thingPosOnBaseMap)))
                    {
                        return false;
                    }
                    if (!searcherPawn.CanSee(thing, losValidator))
                    {
                        if (t is Pawn)
                        {
                            if ((flags & TargetScanFlags.NeedLOSToPawns) != TargetScanFlags.None)
                            {
                                return false;
                            }
                        }
                        else if ((flags & TargetScanFlags.NeedLOSToNonPawns) != TargetScanFlags.None)
                        {
                            return false;
                        }
                    }
                }
                if (((flags & TargetScanFlags.NeedThreat) != TargetScanFlags.None || (flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None) && t.ThreatDisabled(searcherPawn))
                {
                    return false;
                }
                if ((flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None && !AttackTargetFinder.IsAutoTargetable(t))
                {
                    return false;
                }
                if ((flags & TargetScanFlags.NeedActiveThreat) != TargetScanFlags.None && !GenHostility.IsActiveThreatTo(t, searcherPawn.Faction))
                {
                    return false;
                }
                Pawn pawn = t as Pawn;
                if ((flags & TargetScanFlags.NeedNonBurning) != TargetScanFlags.None && thing.IsBurning())
                {
                    return false;
                }

                if (thing.def.size.x == 1 && thing.def.size.z == 1)
                {
                    if (thingPosOnBaseMap.Fogged(baseMap))
                    {
                        return false;
                    }
                }
                else
                {
                    bool flag2 = false;
                    foreach (var c in thing.OccupiedRect())
                    {
                        if (!c.Fogged(baseMap))
                        {
                            flag2 = true;
                            break;
                        }
                    }
                    if (!flag2)
                    {
                        return false;
                    }
                }
                return true;
            }

            List<IAttackTarget> tmpTargets = new List<IAttackTarget>();
            var maps = searcherPawn.Map.BaseMapAndVehicleMaps().Except(searcherPawn.Map);
            tmpTargets.AddRange(maps.SelectMany(m => m.attackTargetsCache.GetPotentialTargetsFor(searcherPawn)));

            bool flag = false;
            for (int i = 0; i < tmpTargets.Count; i++)
            {
                IAttackTarget attackTarget = tmpTargets[i];
                var thingPosOnBaseMap = attackTarget.Thing.PositionOnBaseMap();
                if (thingPosOnBaseMap.InHorDistOf(searcherPosOnBaseMap, maxDist) && innerValidator(attackTarget) && VehicleTurret.TryFindShootLineFromTo(searcherPosOnBaseMap, new LocalTargetInfo(attackTarget.Thing), out ShootLine resultingLine))
                {
                    flag = true;
                    break;
                }
            }
            IAttackTarget result;
            if (flag)
            {
                tmpTargets.RemoveAll((IAttackTarget x) => !x.Thing.PositionOnBaseMap().InHorDistOf(searcherPosOnBaseMap, maxDist) || !innerValidator(x));
                result = TargetingHelper.GetRandomShootingTargetByScore(tmpTargets, searcherPawn);
            }
            else
            {
                Predicate<Thing> validator2;
                if ((flags & TargetScanFlags.NeedReachableIfCantHitFromMyPos) != TargetScanFlags.None && (flags & TargetScanFlags.NeedReachable) == TargetScanFlags.None)
                {
                    validator2 = ((Thing t) => innerValidator((IAttackTarget)t) && VehicleTurret.TryFindShootLineFromTo(searcherPawn.PositionOnBaseMap(), new LocalTargetInfo(t), out ShootLine resultingLine));
                }
                else
                {
                    validator2 = ((Thing t) => innerValidator((IAttackTarget)t));
                }
                result = (IAttackTarget)GenClosestOnVehicle.ClosestThing_Global(searcherPawn.PositionOnBaseMap(), tmpTargets, maxDist, validator2, null);
            }
            tmpTargets.Clear();
            return result;
        }

        /// <summary>
        /// Get random target by weight
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="searcher"></param>
        public static IAttackTarget GetRandomShootingTargetByScore(List<IAttackTarget> targets, VehiclePawn searcher)
        {
            if (GetAvailableShootingTargetsByScore(targets, searcher).TryRandomElementByWeight(
                (Pair<IAttackTarget, float> x) => x.Second, out Pair<IAttackTarget, float> pair))
            {
                return pair.First;
            }
            return null;
        }

        /// <summary>
        /// Get all available targets ordered by weight
        /// </summary>
        /// <param name="rawTargets"></param>
        /// <param name="searcher"></param>
        /// <returns></returns>
        public static List<Pair<IAttackTarget, float>> GetAvailableShootingTargetsByScore(List<IAttackTarget> rawTargets, VehiclePawn searcher)
        {
            List<Pair<IAttackTarget, float>> availableShootingTargets = new List<Pair<IAttackTarget, float>>();
            List<float> tmpTargetScores = new List<float>();
            List<bool> tmpCanShootAtTarget = new List<bool>();
            if (rawTargets.Count == 0)
            {
                return availableShootingTargets;
            }
            tmpTargetScores.Clear();
            tmpCanShootAtTarget.Clear();
            float num = 0f;
            IAttackTarget attackTarget = null;
            for (int i = 0; i < rawTargets.Count; i++)
            {
                tmpTargetScores.Add(float.MinValue);
                tmpCanShootAtTarget.Add(false);
                if (rawTargets[i] != searcher)
                {
                    bool flag = VehicleTurret.TryFindShootLineFromTo(searcher.PositionOnBaseMap(), new LocalTargetInfo(rawTargets[i].Thing.PositionOnBaseMap()), out ShootLine shootLine);
                    tmpCanShootAtTarget[i] = flag;
                    if (flag)
                    {
                        float shootingTargetScore = GetShootingTargetScore(rawTargets[i], searcher);
                        tmpTargetScores[i] = shootingTargetScore;
                        if (attackTarget == null || shootingTargetScore > num)
                        {
                            attackTarget = rawTargets[i];
                            num = shootingTargetScore;
                        }
                    }
                }
            }
            if (num < 1f)
            {
                if (attackTarget != null)
                {
                    availableShootingTargets.Add(new Pair<IAttackTarget, float>(attackTarget, 1f));
                }
            }
            else
            {
                float num2 = num - 30f;
                for (int j = 0; j < rawTargets.Count; j++)
                {
                    if (rawTargets[j] != searcher && tmpCanShootAtTarget[j])
                    {
                        float num3 = tmpTargetScores[j];
                        if (num3 >= num2)
                        {
                            float second = Mathf.InverseLerp(num - 30f, num, num3);
                            availableShootingTargets.Add(new Pair<IAttackTarget, float>(rawTargets[j], second));
                        }
                    }
                }
            }
            return availableShootingTargets;
        }

        /// <summary>
        /// Get target score
        /// </summary>
        /// <param name="target"></param>
        /// <param name="searcher"></param>
        private static float GetShootingTargetScore(IAttackTarget target, IAttackTargetSearcher searcher)
        {
            float num = 60f;
            num -= Mathf.Min((target.Thing.PositionOnBaseMap() - searcher.Thing.PositionOnBaseMap()).LengthHorizontal, 40f);
            if (target.TargetCurrentlyAimingAt == searcher.Thing)
            {
                num += 10f;
            }
            if (searcher.LastAttackedTarget == target.Thing && Find.TickManager.TicksGame - searcher.LastAttackTargetTick <= 300)
            {
                num += 40f;
            }
            num -= CoverUtility.CalculateOverallBlockChance(target.Thing.Position, searcher.Thing.PositionOnAnotherThingMap(target.Thing), target.Thing.Map) * 10f;
            if (target is Pawn pawn && pawn.RaceProps.Animal && pawn.Faction != null && !pawn.IsFighting())
            {
                num -= 50f;
            }
            //num += _  - add additional cost based on how close to friendly fire
            return num * target.TargetPriorityFactor;
        }
    }
}
