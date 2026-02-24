using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class NonSnapAttackTargetFinderOnVehicle
{
    private const float FriendlyFireScoreOffsetPerHumanlikeOrMechanoid = 18f;

    private const float FriendlyFireScoreOffsetPerAnimal = 7f;

    private const float FriendlyFireScoreOffsetPerNonPawn = 10f;

    private const float FriendlyFireScoreOffsetSelf = 40f;

    private static readonly List<IAttackTarget> tmpTargets = new(128);

    private static readonly List<Pair<IAttackTarget, float>> availableShootingTargets = [];

    private static readonly List<float> tmpTargetScores = [];

    private static readonly List<bool> tmpCanShootAtTarget = [];

    public static IAttackTarget BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags, Vector3 angle, Predicate<Thing> validator = null, float minDist = 0f, float maxDist = 9999f)
    {
        var searcherThing = searcher.Thing;
        var verb = searcher.CurrentEffectiveVerb;
        if (verb == null)
        {
            Log.Error("BestAttackTarget with " + searcher.ToStringSafe() + " who has no attack verb.");
            return null;
        }
        var onlyTargetMachines = verb.IsEMP();
        var minDistSquared = minDist * minDist;
        Func<IntVec3, bool> losValidator = null;
        if ((flags & TargetScanFlags.LOSBlockableByGas) != 0)
        {
            losValidator = vec3 => !vec3.AnyGas(searcherThing.GroundMap, GasType.BlindSmoke);
        }



        tmpTargets.Clear();
        foreach (var map in searcherThing.Map.BaseMapAndVehicleMaps(false))
        {
            tmpTargets.AddRange(map.attackTargetsCache.GetPotentialTargetsFor(searcher));
        }
        tmpTargets.RemoveAll(t => ShouldIgnoreNoncombatant(searcherThing, t, flags));
        var flag = false;
        for (var i = 0; i < tmpTargets.Count; i++)
        {
            var attackTarget = tmpTargets[i];
            if (attackTarget.Thing.PositionOnBaseMap.InHorDistOf(searcherThing.PositionOnBaseMap, maxDist) && innerValidator(attackTarget) && CanShootAtFromCurrentPosition(attackTarget, searcher, verb))
            {
                flag = true;
                break;
            }
        }
        IAttackTarget result;
        if (flag)
        {
            tmpTargets.RemoveAll(x => !x.Thing.PositionOnBaseMap.InHorDistOf(searcherThing.PositionOnBaseMap, maxDist) || !innerValidator(x));
            result = GetRandomShootingTargetByScore(tmpTargets, searcher, verb, angle);
        }
        else
        {
            var num2 = (flags & TargetScanFlags.NeedReachableIfCantHitFromMyPos) != 0;
            var flag2 = (flags & TargetScanFlags.NeedReachable) != 0;
            result = (IAttackTarget)GenClosestCrossMap.ClosestThing_Global(validator: (!num2 || flag2) ? t => innerValidator((IAttackTarget)t) : t => innerValidator((IAttackTarget)t) && CanShootAtFromCurrentPosition((IAttackTarget)t, searcher, verb), centerOnBaseMap: searcherThing.PositionOnBaseMap, searchSet: tmpTargets, maxDistance: maxDist);
        }
        tmpTargets.Clear();
        return result;

        bool innerValidator(IAttackTarget t)
        {
            var thing = t.Thing;
            if (t == searcher)
            {
                return false;
            }

            if (minDistSquared > 0f && (searcherThing.PositionOnBaseMap - thing.PositionOnBaseMap).LengthHorizontalSquared < minDistSquared)
            {
                return false;
            }

            var num3 = verb.verbProps.EffectiveMinRange(thing, searcherThing);
            if (num3 > 0f && (searcherThing.PositionOnBaseMap - thing.PositionOnBaseMap).LengthHorizontalSquared < num3 * num3)
            {
                return false;
            }

            if (!searcherThing.HostileTo(thing))
            {
                return false;
            }

            if (validator != null && !validator(thing))
            {
                return false;
            }

            if ((flags & TargetScanFlags.NeedNotUnderThickRoof) != 0)
            {
                var roof = thing.PositionOnBaseMap.GetRoof(thing.GroundMap);
                if (roof is { isThickRoof: true })
                {
                    return false;
                }
            }
            if ((flags & TargetScanFlags.NeedLOSToAll) != 0)
            {
                if (losValidator != null && (!losValidator(searcherThing.PositionOnBaseMap) || !losValidator(thing.PositionOnBaseMap)))
                {
                    return false;
                }

                if (!searcherThing.CanSee(thing, losValidator))
                {
                    if (t is Pawn)
                    {
                        if ((flags & TargetScanFlags.NeedLOSToPawns) != 0)
                        {
                            return false;
                        }
                    }
                    else if ((flags & TargetScanFlags.NeedLOSToNonPawns) != 0)
                    {
                        return false;
                    }
                }
            }
            if (((flags & TargetScanFlags.NeedThreat) != 0 || (flags & TargetScanFlags.NeedAutoTargetable) != 0) && t.ThreatDisabled(searcher))
            {
                return false;
            }

            if ((flags & TargetScanFlags.NeedAutoTargetable) != 0 && !AttackTargetFinder.IsAutoTargetable(t))
            {
                return false;
            }

            if ((flags & TargetScanFlags.NeedActiveThreat) != 0 && !GenHostility.IsActiveThreatTo(t, searcher.Thing.Faction))
            {
                return false;
            }

            if (onlyTargetMachines && t is Pawn pawn && pawn.RaceProps.IsFlesh)
            {
                return false;
            }

            if ((flags & TargetScanFlags.NeedNonBurning) != 0 && thing.IsBurning())
            {
                return false;
            }

            if (searcherThing.def.race != null && (int)searcherThing.def.race.intelligence >= 2)
            {
                var compExplosive = thing.TryGetComp<CompExplosive>();
                if (compExplosive is { wickStarted: true })
                {
                    return false;
                }
            }
            if (thing.def.size is { x: 1, z: 1 })
            {
                if (thing.PositionOnBaseMap.Fogged(thing.GroundMap))
                {
                    return false;
                }
            }
            else
            {
                var flag3 = false;
                foreach (var item in thing.MovedOccupiedRect())
                {
                    if (!item.Fogged(thing.GroundMap))
                    {
                        flag3 = true;
                        break;
                    }
                }
                if (!flag3)
                {
                    return false;
                }
            }
            return true;
        }
    }

    private static bool ShouldIgnoreNoncombatant(Thing searcherThing, IAttackTarget t, TargetScanFlags flags)
    {
        if (t is not Pawn pawn)
        {
            return false;
        }

        if (pawn.IsCombatant())
        {
            return false;
        }

        if ((flags & TargetScanFlags.IgnoreNonCombatants) != 0)
        {
            return true;
        }

        return !GenSightOnVehicle.LineOfSightToThing(searcherThing.PositionOnBaseMap, pawn, searcherThing.GroundMap);
    }

    private static bool CanShootAtFromCurrentPosition(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb)
    {
        return verb?.CanHitTargetFrom(searcher.Thing.PositionOnBaseMap, target.Thing) ?? false;
    }

    private static IAttackTarget GetRandomShootingTargetByScore(List<IAttackTarget> targets, IAttackTargetSearcher searcher, Verb verb, Vector3 angle)
    {
        return GetAvailableShootingTargetsByScore(targets, searcher, verb, angle)
            .TryRandomElementByWeight(x => x.Second, out var result)
            ? result.First
            : null;
    }

    private static List<Pair<IAttackTarget, float>> GetAvailableShootingTargetsByScore(List<IAttackTarget> rawTargets, IAttackTargetSearcher searcher, Verb verb, Vector3 angle)
    {
        availableShootingTargets.Clear();
        if (rawTargets.Count == 0)
        {
            return availableShootingTargets;
        }
        tmpTargetScores.Clear();
        tmpCanShootAtTarget.Clear();
        var num = 0f;
        IAttackTarget attackTarget = null;

        for (var i = 0; i < rawTargets.Count; i++)
        {
            tmpTargetScores.Add(float.MinValue);
            tmpCanShootAtTarget.Add(item: false);
            if (rawTargets[i] == searcher)
            {
                continue;
            }
            var flag = CanShootAtFromCurrentPosition(rawTargets[i], searcher, verb);
            tmpCanShootAtTarget[i] = flag;
            if (flag)
            {
                var shootingTargetScore = GetShootingTargetScore(rawTargets[i], searcher, verb, angle);
                tmpTargetScores[i] = shootingTargetScore;
                if (attackTarget == null || shootingTargetScore > num)
                {
                    attackTarget = rawTargets[i];
                    num = shootingTargetScore;
                }
            }
        }
        for (var j = 0; j < rawTargets.Count; j++)
        {
            if (rawTargets[j] != searcher && tmpCanShootAtTarget[j])
            {
                availableShootingTargets.Add(new Pair<IAttackTarget, float>(rawTargets[j], tmpTargetScores[j]));
            }
        }
        return availableShootingTargets;
    }

    private static float GetShootingTargetScore(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb, Vector3 angle)
    {
        var num = 60f;
        num -= Mathf.Min((target.Thing.PositionOnBaseMap - searcher.Thing.PositionOnBaseMap).LengthHorizontal, FriendlyFireScoreOffsetSelf);
        if (target.TargetCurrentlyAimingAt == searcher.Thing)
        {
            num += FriendlyFireScoreOffsetPerNonPawn;
        }
        if (searcher.LastAttackedTarget == target.Thing && Find.TickManager.TicksGame - searcher.LastAttackTargetTick <= 300)
        {
            num += FriendlyFireScoreOffsetSelf;
        }
        num -= CoverUtility.CalculateOverallBlockChance(target.Thing.Position, searcher.Thing.PositionOnAnotherThingMap(target.Thing), searcher.Thing.Map) * 10f;
        if (target is Pawn pawn)
        {
            num -= NonCombatantScore(pawn);
            if (verb.verbProps.ai_TargetHasRangedAttackScoreOffset != 0f && pawn.CurrentEffectiveVerb != null && pawn.CurrentEffectiveVerb.verbProps.Ranged)
            {
                num += verb.verbProps.ai_TargetHasRangedAttackScoreOffset;
            }
            if (pawn.Downed)
            {
                num -= 50f;
            }
        }
        num += FriendlyFireBlastRadiusTargetScoreOffset(target, searcher, verb);
        num += FriendlyFireConeTargetScoreOffset(target, searcher, verb);

        var ext = searcher.Thing.def.GetModExtension<NonSnapTurretExtension>();
        var anglef = Vector3.Angle(angle, (target.Thing.DrawPos - searcher.Thing.DrawPos).Yto0());
        if (ext == null)
        {
            if (anglef < 0.1f)
            {
                anglef = 0.1f;
            }

            return num / anglef;
        }
        return ext.TweakWeight(num * target.TargetPriorityFactor, anglef);
    }

    private static float NonCombatantScore(Thing target)
    {
        if (target is not Pawn pawn)
        {
            return 0f;
        }
        if (!pawn.IsCombatant())
        {
            return 50f;
        }
        return pawn.DevelopmentalStage.Juvenile() ? 25f : 0f;
    }

    private static float FriendlyFireBlastRadiusTargetScoreOffset(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb)
    {
        if (verb.verbProps.ai_AvoidFriendlyFireRadius <= 0f)
        {
            return 0f;
        }
        var map = target.Thing.GroundMap;
        var position = target.Thing.PositionOnBaseMap;
        var num = GenRadial.NumCellsInRadius(verb.verbProps.ai_AvoidFriendlyFireRadius);
        var num2 = 0f;
        for (var i = 0; i < num; i++)
        {
            var intVec = position + GenRadial.RadialPattern[i];
            if (!intVec.InBounds(map))
            {
                continue;
            }
            var flag = true;
            var thingList = intVec.GetThingListAcrossMaps(map);
            for (var j = 0; j < thingList.Count; j++)
            {
                if (thingList[j] is not IAttackTarget || thingList[j] == target)
                {
                    continue;
                }
                if (flag)
                {
                    if (!GenSightOnVehicle.LineOfSight(position, intVec, map, skipFirstCell: true))
                    {
                        break;
                    }
                    flag = false;
                }
                var num3 = ((thingList[j] == searcher) ? FriendlyFireScoreOffsetSelf : ((thingList[j] is not Pawn) ? 10f : (thingList[j].def.race.Animal ? FriendlyFireScoreOffsetPerAnimal : FriendlyFireScoreOffsetPerHumanlikeOrMechanoid)));
                num2 = ((!searcher.Thing.HostileTo(thingList[j])) ? (num2 - num3) : (num2 + num3 * 0.6f));
            }
        }
        return num2;
    }

    private static float FriendlyFireConeTargetScoreOffset(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb)
    {
        if (searcher.Thing is not Pawn pawn)
        {
            return 0f;
        }
        if ((int)pawn.RaceProps.intelligence < 1)
        {
            return 0f;
        }
        if (pawn.RaceProps.IsMechanoid)
        {
            return 0f;
        }
        if (verb is not Verb_Shoot verb_Shoot)
        {
            return 0f;
        }
        var defaultProjectile = verb_Shoot.verbProps.defaultProjectile;
        if (defaultProjectile == null)
        {
            return 0f;
        }
        if (defaultProjectile.projectile.flyOverhead)
        {
            return 0f;
        }
        var map = pawn.GroundMap;
        var report = ShotReport.HitReportFor(pawn, verb, (Thing)target);
        var radius = Mathf.Max(VerbUtility.CalculateAdjustedForcedMiss(verb.verbProps.ForcedMissRadius, report.ShootLine.Dest - report.ShootLine.Source), 1.5f);
        var enumerable = (from dest in GenRadial.RadialCellsAround(report.ShootLine.Dest, radius, useCenter: true)
                                           where dest.InBounds(map)
                                           select new ShootLine(report.ShootLine.Source, dest)).SelectMany(line => line.Points().Concat(line.Dest).TakeWhile(pos => pos.CanBeSeenOverFast(map))).Distinct();
        var num = 0f;
        foreach (var item in enumerable)
        {
            var num2 = VerbUtility.InterceptChanceFactorFromDistance(report.ShootLine.Source.ToVector3Shifted(), item);
            if (num2 <= 0f)
            {
                continue;
            }
            var thingList = item.GetThingListAcrossMaps(map);
            for (var i = 0; i < thingList.Count; i++)
            {
                var thing = thingList[i];
                if (thing is IAttackTarget && thing != target)
                {
                    var num3 = ((thing == searcher) ? FriendlyFireScoreOffsetSelf : ((thing is not Pawn) ? 10f : (thing.def.race.Animal ? FriendlyFireScoreOffsetPerAnimal : FriendlyFireScoreOffsetPerHumanlikeOrMechanoid)));
                    num3 *= num2;
                    num3 = ((!searcher.Thing.HostileTo(thing)) ? (num3 * -1f) : (num3 * 0.6f));
                    num += num3;
                }
            }
        }
        return num;
    }
}