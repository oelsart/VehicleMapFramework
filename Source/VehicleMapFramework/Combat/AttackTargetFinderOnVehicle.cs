﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

public static class AttackTargetFinderOnVehicle
{
    private const float FriendlyFireScoreOffsetPerHumanlikeOrMechanoid = 18f;

    private const float FriendlyFireScoreOffsetPerAnimal = 7f;

    private const float FriendlyFireScoreOffsetPerNonPawn = 10f;

    private const float FriendlyFireScoreOffsetSelf = 40f;

    private static List<IAttackTarget> tmpTargets = new(128);

    private static List<IAttackTarget> validTargets = [];

    //private static List<CompProjectileInterceptor> interceptors;

    private static List<Pair<IAttackTarget, float>> availableShootingTargets = [];

    private static List<float> tmpTargetScores = [];

    private static List<bool> tmpCanShootAtTarget = [];

    private static List<IntVec3> tempDestList = [];

    private static List<IntVec3> tempSourceList = [];

    public static IAttackTarget BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator = null, float minDist = 0f, float maxDist = 9999f, IntVec3 locus = default, float maxTravelRadiusFromLocus = 3.4028235E+38f, bool canBashDoors = false, bool canTakeTargetsCloserThanEffectiveMinRange = true, bool canBashFences = false, bool onlyRanged = false)
    {
        //AttackTargetFinderOnVehicle.interceptors = searcher.Thing?.Map.BaseMapAndVehicleMaps()
        //    .SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor)
        //    .Select(t => t.TryGetComp<CompProjectileInterceptor>())).ToList() ?? new List<CompProjectileInterceptor>();
        var searcherThing = searcher.Thing;
        var searcherPawn = searcher as Pawn;
        var verb = searcher.CurrentEffectiveVerb;
        if (verb == null)
        {
            Log.Error("BestAttackTarget with " + searcher.ToStringSafe() + " who has no attack verb.");
            return null;
        }
        var onlyTargetMachines = !CombatExtended.Active && verb.IsEMP();
        var minDistSquared = minDist * minDist;
        float num = maxTravelRadiusFromLocus + verb.verbProps.range;
        var maxLocusDistSquared = num * num;
        Func<IntVec3, bool> losValidator = null;
        if ((flags & TargetScanFlags.LOSBlockableByGas) != TargetScanFlags.None)
        {
            losValidator = vec3 => !vec3.InBounds(searcherThing.BaseMap()) || !vec3.AnyGas(searcherThing.BaseMap(), GasType.BlindSmoke);
        }
        Predicate<IAttackTarget> innerValidator = delegate (IAttackTarget t)
        {
            Thing thing = t.Thing;
            var baseMap = thing.BaseMap();
            if (t == searcher)
            {
                return false;
            }
            if (minDistSquared > 0f && (searcherThing.PositionOnBaseMap() - thing.PositionOnBaseMap()).LengthHorizontalSquared < minDistSquared)
            {
                return false;
            }
            if (!canTakeTargetsCloserThanEffectiveMinRange)
            {
                float num2 = verb.verbProps.EffectiveMinRange(thing, searcherThing);
                if (num2 > 0f && (searcherThing.PositionOnBaseMap() - thing.PositionOnBaseMap()).LengthHorizontalSquared < num2 * num2)
                {
                    return false;
                }
            }
            if (maxTravelRadiusFromLocus < 9999f && (thing.PositionOnBaseMap() - locus).LengthHorizontalSquared > maxLocusDistSquared)
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
            if (searcherPawn != null)
            {
                Lord lord = searcherPawn.GetLord();
                if (lord != null && !lord.LordJob.ValidateAttackTarget(searcherPawn, thing))
                {
                    return false;
                }
            }
            if ((flags & TargetScanFlags.NeedNotUnderThickRoof) != TargetScanFlags.None)
            {
                RoofDef roof = thing.PositionOnBaseMap().GetRoof(baseMap);
                if (roof != null && roof.isThickRoof)
                {
                    return false;
                }
            }
            if ((flags & TargetScanFlags.NeedLOSToAll) != TargetScanFlags.None)
            {
                if (losValidator != null && (!losValidator(searcherThing.PositionOnBaseMap()) || !losValidator(thing.PositionOnBaseMap())))
                {
                    return false;
                }
                if (!searcherThing.CanSee(thing, losValidator))
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
            if (((flags & TargetScanFlags.NeedThreat) != TargetScanFlags.None || (flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None) && t.ThreatDisabled(searcher))
            {
                return false;
            }
            if ((flags & TargetScanFlags.NeedAutoTargetable) != TargetScanFlags.None && !IsAutoTargetable(t))
            {
                return false;
            }
            if ((flags & TargetScanFlags.NeedActiveThreat) != TargetScanFlags.None && !GenHostility.IsActiveThreatTo(t, searcher.Thing.Faction))
            {
                return false;
            }
            if (onlyTargetMachines && t is Pawn pawn && pawn.RaceProps.IsFlesh)
            {
                return false;
            }
            if ((flags & TargetScanFlags.NeedNonBurning) != TargetScanFlags.None && thing.IsBurning())
            {
                return false;
            }
            if (searcherThing.def.race != null && searcherThing.def.race.intelligence >= Intelligence.Humanlike)
            {
                CompExplosive compExplosive = thing.TryGetComp<CompExplosive>();
                if (compExplosive != null && compExplosive.wickStarted)
                {
                    return false;
                }
            }
            if (thing.def.size.x == 1 && thing.def.size.z == 1)
            {
                if (thing.PositionOnBaseMap().Fogged(baseMap))
                {
                    return false;
                }
            }
            else
            {
                bool flag4 = false;
                foreach (var c in thing.MovedOccupiedRect())
                {
                    if (!c.Fogged(baseMap))
                    {
                        flag4 = true;
                        break;
                    }
                }
                if (!flag4)
                {
                    return false;
                }
            }
            return true;
        };
        if ((HasRangedAttack(searcher) || onlyRanged) && (searcherPawn == null || !searcherPawn.InAggroMentalState))
        {
            tmpTargets.Clear();
            tmpTargets.AddRange(searcherThing.Map.BaseMapAndVehicleMaps().Except(searcherThing.Map).SelectMany(m => m.attackTargetsCache.GetPotentialTargetsFor(searcher)));
            validTargets.Clear();
            for (int i = 0; i < tmpTargets.Count; i++)
            {
                IAttackTarget attackTarget = tmpTargets[i];
                if (attackTarget.Thing.PositionOnBaseMap().InHorDistOf(searcherThing.PositionOnBaseMap(), maxDist) && innerValidator(attackTarget))
                {
                    validTargets.Add(attackTarget);
                }
            }
            if (validTargets.Count == 0)
            {
                return null;
            }

            var targetToHit = GetRandomShootingTargetByScore(validTargets, searcher, verb);
            if (targetToHit != null || searcher is Building_Turret || (searcher is Pawn sercherPawn && searcherPawn.CurJobDef == JobDefOf.ManTurret))
            {
                return targetToHit;
            }
            if (flags.HasFlag(TargetScanFlags.NeedReachableIfCantHitFromMyPos) || flags.HasFlag(TargetScanFlags.NeedReachable))
            {
                return (IAttackTarget)GenClosestCrossMap.ClosestThing_Global(
                    searcher.Thing.PositionOnBaseMap(),
                    validTargets,
                    maxDist,
                    t => CanReach(searcher.Thing, t, canBashDoors, canBashFences),
                    null,
                    false
                    );
            }
            return (IAttackTarget)GenClosestCrossMap.ClosestThing_Global(searcher.Thing.PositionOnBaseMap(), validTargets, maxDist, null, null, false);
        }
        if (searcherPawn != null && searcherPawn.mindState.duty != null && searcherPawn.mindState.duty.radius > 0f && !searcherPawn.InMentalState)
        {
            Predicate<IAttackTarget> oldValidator = innerValidator;
            innerValidator = t =>
            {
                return oldValidator(t) && t.Thing.PositionOnBaseMap().InHorDistOf(searcherPawn.mindState.duty.focus.CellOnBaseMap(), searcherPawn.mindState.duty.radius);
            };
        }
        Predicate<IAttackTarget> oldValidator2 = innerValidator;
        innerValidator = t =>
        {
            return t.Thing.Map != searcherThing.Map && oldValidator2(t) && !ShouldIgnoreNoncombatant(searcherThing, t, flags);
        };
        IAttackTarget attackTarget2 = (IAttackTarget)GenClosestCrossMap.ClosestThingReachable(searcherThing.Position, searcherThing.Map, ThingRequest.ForGroup(ThingRequestGroup.AttackTarget), PathEndMode.Touch, TraverseParms.For(searcherPawn, Danger.Deadly, TraverseMode.ByPawn, canBashDoors, false, canBashFences), maxDist, x => innerValidator((IAttackTarget)x), null, 0, (maxDist > 800f) ? -1 : 40, false, RegionType.Set_Passable, false);
        //if (attackTarget2 != null && PawnUtility.ShouldCollideWithPawns(searcherPawn))
        //{
        //    IAttackTarget attackTarget3 = FindBestReachableMeleeTarget(innerValidator, searcherPawn, maxDist, canBashDoors, canBashFences);
        //    if (attackTarget3 != null)
        //    {
        //        float lengthHorizontal = (searcherPawn.PositionOnBaseMap() - attackTarget2.Thing.PositionOnBaseMap()).LengthHorizontal;
        //        float lengthHorizontal2 = (searcherPawn.PositionOnBaseMap() - attackTarget3.Thing.PositionOnBaseMap()).LengthHorizontal;
        //        if (Mathf.Abs(lengthHorizontal - lengthHorizontal2) < 50f)
        //        {
        //            attackTarget2 = attackTarget3;
        //        }
        //    }
        //}

        return attackTarget2;
    }

    private static bool ShouldIgnoreNoncombatant(Thing searcherThing, IAttackTarget t, TargetScanFlags flags)
    {
        return t is Pawn pawn && !pawn.IsCombatant() && ((flags & TargetScanFlags.IgnoreNonCombatants) != TargetScanFlags.None || !GenSightOnVehicle.LineOfSightThingToThing(searcherThing, pawn, false, null));
    }

    private static bool CanReach(Thing searcher, Thing target, bool canBashDoors, bool canBashFences)
    {
        if (searcher is Pawn pawn)
        {
            if (!pawn.CanReach(target, PathEndMode.Touch, Danger.Some, canBashDoors, canBashFences, TraverseMode.ByPawn, target.Map, out _, out _))
            {
                return false;
            }
        }
        else
        {
            TraverseMode mode = canBashDoors ? TraverseMode.PassDoors : TraverseMode.NoPassClosedDoors;
            if (!CrossMapReachabilityUtility.CanReach(searcher.Map, searcher.Position, target, PathEndMode.Touch, TraverseParms.For(mode, Danger.Deadly, false, false, false), target.Map, out _, out _))
            {
                return false;
            }
        }
        return true;
    }

    private static IAttackTarget FindBestReachableMeleeTarget(Predicate<IAttackTarget> validator, Pawn searcherPawn, float maxTargDist, bool canBashDoors, bool canBashFences)
    {
        maxTargDist = Mathf.Min(maxTargDist, 30f);
        IAttackTarget reachableTarget = null;
        IAttackTarget bestTargetOnCell(IntVec3 x)
        {
            List<Thing> thingList = x.GetThingList(searcherPawn.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                if (thing is IAttackTarget attackTarget && validator(attackTarget) && ReachabilityImmediate.CanReachImmediate(x, thing, searcherPawn.Map, PathEndMode.Touch, searcherPawn) && (searcherPawn.CanReachImmediate(thing, PathEndMode.Touch) || searcherPawn.Map.attackTargetReservationManager.CanReserve(searcherPawn, attackTarget)))
                {
                    return attackTarget;
                }
            }
            return null;
        }
        searcherPawn.Map.floodFiller.FloodFill(searcherPawn.Position, delegate (IntVec3 x)
        {
            if (!x.WalkableBy(searcherPawn.Map, searcherPawn))
            {
                return false;
            }
            if (x.DistanceToSquared(searcherPawn.Position) > maxTargDist * maxTargDist)
            {
                return false;
            }
            Building edifice = x.GetEdifice(searcherPawn.Map);
            if (edifice != null)
            {
                if (!canBashDoors && edifice is Building_Door building_Door && !building_Door.CanPhysicallyPass(searcherPawn))
                {
                    return false;
                }
                if (!canBashFences && edifice.def.IsFence && searcherPawn.def.race.FenceBlocked)
                {
                    return false;
                }
            }
            return !PawnUtility.AnyPawnBlockingPathAt(x, searcherPawn, true, false, false);
        }, delegate (IntVec3 x)
        {
            for (int i = 0; i < 8; i++)
            {
                IntVec3 intVec = x + GenAdj.AdjacentCells[i];
                if (intVec.InBounds(searcherPawn.Map))
                {
                    IAttackTarget attackTarget = bestTargetOnCell(intVec);
                    if (attackTarget != null)
                    {
                        reachableTarget = attackTarget;
                        break;
                    }
                }
            }
            return reachableTarget != null;
        }, int.MaxValue, false, null);
        return reachableTarget;
    }

    private static bool HasRangedAttack(IAttackTargetSearcher t)
    {
        Verb currentEffectiveVerb = t.CurrentEffectiveVerb;
        return currentEffectiveVerb != null && !currentEffectiveVerb.verbProps.IsMeleeAttack;
    }

    private static bool CanShootAtFromCurrentPosition(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb)
    {
        return verb != null && verb.CanHitTargetFrom(searcher.Thing.PositionOnBaseMap(), target.Thing);
    }

    private static IAttackTarget GetRandomShootingTargetByScore(List<IAttackTarget> targets, IAttackTargetSearcher searcher, Verb verb)
    {
        if (GetAvailableShootingTargetsByScore(targets, searcher, verb).TryRandomElementByWeight(x => x.Second, out Pair<IAttackTarget, float> pair))
        {
            return pair.First;
        }
        return null;
    }

    private static List<Pair<IAttackTarget, float>> GetAvailableShootingTargetsByScore(List<IAttackTarget> rawTargets, IAttackTargetSearcher searcher, Verb verb)
    {
        availableShootingTargets.Clear();
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
                bool flag = CanShootAtFromCurrentPosition(rawTargets[i], searcher, verb);
                tmpCanShootAtTarget[i] = flag;
                if (flag)
                {
                    float shootingTargetScore = GetShootingTargetScore(rawTargets[i], searcher, verb);
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

    private static float GetShootingTargetScore(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb)
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
        return num * target.TargetPriorityFactor;
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
        if (pawn.DevelopmentalStage.Juvenile())
        {
            return 25f;
        }
        return 0f;
    }

    private static float FriendlyFireBlastRadiusTargetScoreOffset(IAttackTarget target, IAttackTargetSearcher searcher, Verb verb)
    {
        var score = FriendlyFireBlastRadiusTargetScoreOffset(target, searcher, target.Thing.Map, verb);
        if (target.Thing.Map != searcher.Thing.Map)
        {
            score += FriendlyFireBlastRadiusTargetScoreOffset(target, searcher, searcher.Thing.Map, verb);
        }
        return score;
    }

    private static float FriendlyFireBlastRadiusTargetScoreOffset(IAttackTarget target, IAttackTargetSearcher searcher, Map map, Verb verb)
    {
        if (verb.verbProps.ai_AvoidFriendlyFireRadius <= 0f)
        {
            return 0f;
        }
        IntVec3 position = target.Thing.Map == map ? target.Thing.Position : target.Thing.PositionOnAnotherThingMap(searcher.Thing);
        int num = GenRadial.NumCellsInRadius(verb.verbProps.ai_AvoidFriendlyFireRadius);
        float num2 = 0f;
        for (int i = 0; i < num; i++)
        {
            IntVec3 intVec = position + GenRadial.RadialPattern[i];
            if (intVec.InBounds(map))
            {
                bool flag = true;
                List<Thing> thingList = intVec.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    if (thingList[j] is IAttackTarget && thingList[j] != target)
                    {
                        if (flag)
                        {
                            if (!GenSightOnVehicle.LineOfSight(position, intVec, map, true, null, 0, 0))
                            {
                                break;
                            }
                            flag = false;
                        }
                        float num3;
                        if (thingList[j] == searcher)
                        {
                            num3 = 40f;
                        }
                        else if (thingList[j] is Pawn)
                        {
                            num3 = thingList[j].def.race.Animal ? 7f : 18f;
                        }
                        else
                        {
                            num3 = 10f;
                        }
                        if (searcher.Thing.HostileTo(thingList[j]))
                        {
                            num2 += num3 * 0.6f;
                        }
                        else
                        {
                            num2 -= num3;
                        }
                    }
                }
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
        if (pawn.RaceProps.intelligence < Intelligence.ToolUser)
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
        ThingDef defaultProjectile = verb_Shoot.verbProps.defaultProjectile;
        if (defaultProjectile == null)
        {
            return 0f;
        }
        if (defaultProjectile.projectile.flyOverhead)
        {
            return 0f;
        }
        ShotReport report = ShotReport.HitReportFor(pawn, verb, (Thing)target);
        float radius = Mathf.Max(VerbUtility.CalculateAdjustedForcedMiss(verb.verbProps.ForcedMissRadius, report.ShootLine.Dest - report.ShootLine.Source), 1.5f);
        IEnumerable<IntVec3> enumerable = (from dest in GenRadial.RadialCellsAround(report.ShootLine.Dest, radius, true)
                                           select new ShootLine(report.ShootLine.Source, dest)).SelectMany(delegate (ShootLine line)
                                           {
                                               IEnumerable<IntVec3> source = line.Points().Concat(line.Dest);
                                               bool func(IntVec3 pos)
                                               {
                                                   return pos.CanBeSeenOverOnVehicle(pawn.BaseMap());
                                               }
                                               return source.TakeWhile(func);
                                           }).Distinct<IntVec3>();
        float num = 0f;
        foreach (IntVec3 c in enumerable)
        {
            float num2 = VerbUtility.InterceptChanceFactorFromDistance(report.ShootLine.Source.ToVector3Shifted(), c);
            if (num2 > 0f)
            {
                IEnumerable<Thing> thingList = searcher.Thing.Map.thingGrid.ThingsAt(c.ToThingMapCoord(searcher.Thing));
                if (searcher.Thing.Map != target.Thing.Map)
                {
                    thingList = thingList.Concat(target.Thing.Map.thingGrid.ThingsAt(c.ToThingMapCoord(target.Thing)));
                }
                foreach (var thing in thingList)
                {
                    if (thing is IAttackTarget && thing != target)
                    {
                        float num3;
                        if (thing == searcher)
                        {
                            num3 = 40f;
                        }
                        else if (thing is Pawn)
                        {
                            num3 = thing.def.race.Animal ? 7f : 18f;
                        }
                        else
                        {
                            num3 = 10f;
                        }
                        num3 *= num2;
                        if (searcher.Thing.HostileTo(thing))
                        {
                            num3 *= 0.6f;
                        }
                        else
                        {
                            num3 *= -1f;
                        }
                        num += num3;
                    }
                }
            }
        }
        return num;
    }

    public static bool CanSee(this Thing seer, Thing target, Func<IntVec3, bool> validator = null)
    {
        if (seer.Map == target.Map)
        {
            return AttackTargetFinder.CanSee(seer, target, validator);
        }

        var seerPosOnBaseMap = seer.PositionOnBaseMap();
        var targPosOnBaseMap = target.PositionOnBaseMap();
        var baseMap = seer.BaseMap();
        tempDestList.Clear();
        ShootLeanUtilityOnVehicle.CalcShootableCellsOf(tempDestList, target, seerPosOnBaseMap);
        for (int i = 0; i < tempDestList.Count; i++)
        {
            if (GenSightOnVehicle.LineOfSight(seerPosOnBaseMap, tempDestList[i].ToThingBaseMapCoord(target), baseMap, true, validator, 0, 0))
            {
                return true;
            }
        }

        ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(seer.Position, targPosOnBaseMap, seer.Map, tempSourceList);
        for (int j = 0; j < tempSourceList.Count; j++)
        {
            for (int k = 0; k < tempDestList.Count; k++)
            {
                if (GenSightOnVehicle.LineOfSight(tempSourceList[j].ToThingBaseMapCoord(seer), tempDestList[k].ToThingBaseMapCoord(target), baseMap, true, validator, 0, 0))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static void DebugDrawAttackTargetScores_Update()
    {
        if (Find.Selector.SingleSelectedThing is not IAttackTargetSearcher attackTargetSearcher)
        {
            return;
        }
        if (attackTargetSearcher.Thing.Map != Find.CurrentMap)
        {
            return;
        }
        Verb currentEffectiveVerb = attackTargetSearcher.CurrentEffectiveVerb;
        if (currentEffectiveVerb == null)
        {
            return;
        }
        tmpTargets.Clear();
        List<Thing> list = attackTargetSearcher.Thing.Map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
        for (int i = 0; i < list.Count; i++)
        {
            tmpTargets.Add((IAttackTarget)list[i]);
        }
        List<Pair<IAttackTarget, float>> availableShootingTargetsByScore = GetAvailableShootingTargetsByScore(tmpTargets, attackTargetSearcher, currentEffectiveVerb);
        for (int j = 0; j < availableShootingTargetsByScore.Count; j++)
        {
            GenDraw.DrawLineBetween(attackTargetSearcher.Thing.DrawPos, availableShootingTargetsByScore[j].First.Thing.DrawPos);
        }
    }

    public static void DebugDrawAttackTargetScores_OnGUI()
    {
        if (Find.Selector.SingleSelectedThing is not IAttackTargetSearcher attackTargetSearcher)
        {
            return;
        }
        if (attackTargetSearcher.Thing.Map != Find.CurrentMap)
        {
            return;
        }
        Verb currentEffectiveVerb = attackTargetSearcher.CurrentEffectiveVerb;
        if (currentEffectiveVerb == null)
        {
            return;
        }
        List<Thing> list = attackTargetSearcher.Thing.Map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;
        for (int i = 0; i < list.Count; i++)
        {
            Thing thing = list[i];
            if (thing != attackTargetSearcher)
            {
                string text;
                Color red;
                if (!CanShootAtFromCurrentPosition((IAttackTarget)thing, attackTargetSearcher, currentEffectiveVerb))
                {
                    text = "out of range";
                    red = Color.red;
                }
                else
                {
                    text = GetShootingTargetScore((IAttackTarget)thing, attackTargetSearcher, currentEffectiveVerb).ToString("F0");
                    red = new Color(0.25f, 1f, 0.25f);
                }
                GenMapUI.DrawThingLabel(thing.DrawPos.MapToUIPosition(), text, red);
            }
        }
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }
    public static void DebugDrawNonCombatantTimer_OnGUI()
    {
        List<Thing> list = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
        using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleCenter, false))
        {
            foreach (Thing item in list)
            {
                if (!(item is Pawn { mindState: not null } pawn))
                {
                    continue;
                }

                int lastCombatantTick = pawn.mindState.lastCombatantTick;
                Vector2 screenPos = pawn.DrawPos.MapToUIPosition();
                if (pawn.IsCombatant())
                {
                    int num = lastCombatantTick + 3600 - Find.TickManager.TicksGame;
                    if (pawn.IsPermanentCombatant() || num == 3600)
                    {
                        GenMapUI.DrawThingLabel(screenPos, "combatant", Color.red);
                    }
                    else
                    {
                        GenMapUI.DrawThingLabel(screenPos, $"combatant {num}", Color.red);
                    }
                }
                else
                {
                    GenMapUI.DrawThingLabel(screenPos, "non-combatant", Color.green);
                }
            }
        }
    }

    public static bool IsAutoTargetable(IAttackTarget target)
    {
        CompCanBeDormant compCanBeDormant = target.Thing.TryGetComp<CompCanBeDormant>();
        if (compCanBeDormant != null && !compCanBeDormant.Awake)
        {
            return false;
        }
        CompInitiatable compInitiatable = target.Thing.TryGetComp<CompInitiatable>();
        return compInitiatable == null || compInitiatable.Initiated;
    }

    public static IAttackTarget CompareTarget(IAttackTarget target1, IAttackTarget target2, IAttackTargetSearcher searcher)
    {
        if (target1 is null)
        {
            return target2;
        }
        if (target2 is null)
        {
            return target1;
        }
        var priority1 = target1.TargetPriorityFactor;
        var priority2 = target2.TargetPriorityFactor;
        if (priority1 < priority2)
        {
            return target2;
        }
        if (priority1 > priority2)
        {
            return target1;
        }
        if ((target1.Thing.Position - searcher.Thing.Position).LengthHorizontalSquared > (target2.Thing.PositionOnBaseMap() - searcher.Thing.PositionOnBaseMap()).LengthHorizontalSquared)
        {
            return target2;
        }
        return target1;
    }
}

