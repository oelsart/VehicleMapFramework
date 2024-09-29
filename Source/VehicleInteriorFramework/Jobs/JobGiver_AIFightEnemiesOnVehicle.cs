using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors
{
    public class JobGiver_AIFightEnemiesOnVehicle : JobGiver_AIFightEnemy
    {
        protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonyMutant;
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, this.allowTurrets);
            if (verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }
            return CastPositionFinderOnVehicle.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = enemyTarget,
                verb = verb,
                maxRangeFromTarget = verb.verbProps.range,
                wantCoverFromTarget = (verb.verbProps.range > 5f)
            }, out dest);
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var pawnPositionOnBaseMap = pawn.PositionOnBaseMap();
            if ((pawn.IsColonist || pawn.IsColonyMutant) && pawn.playerSettings.hostilityResponse != HostilityResponseMode.Attack)
            {
                Lord lord = pawn.GetLord();
                LordJob_Ritual_Duel lordJob_Ritual_Duel;
                if ((lordJob_Ritual_Duel = (((lord != null) ? lord.LordJob : null) as LordJob_Ritual_Duel)) == null || !lordJob_Ritual_Duel.duelists.Contains(pawn))
                {
                    return null;
                }
            }
            this.UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget == null)
            {
                return null;
            }
            var enemyTargetPositionOnBaseMap = enemyTarget.PositionOnBaseMap();
            Pawn pawn2;
            if ((pawn2 = (enemyTarget as Pawn)) != null && pawn2.IsPsychologicallyInvisible())
            {
                return null;
            }
            bool flag = !pawn.IsColonist && !pawn.IsColonyMutant && !this.DisableAbilityVerbs;
            if (flag)
            {
                Job abilityJob = this.GetAbilityJob(pawn, enemyTarget);
                if (abilityJob != null)
                {
                    return abilityJob;
                }
            }
            if (this.OnlyUseAbilityVerbs)
            {
                IntVec3 intVec;
                if (!this.TryFindShootingPosition(pawn, out intVec, null))
                {
                    return null;
                }
                if (intVec == pawn.Position)
                {
                    return JobMaker.MakeJob(JobDefOf.Wait_Combat, this.ExpiryInterval_Ability.RandomInRange, true);
                }
                Job job = JobMaker.MakeJob(JobDefOf.Goto, intVec);
                job.expiryInterval = this.ExpiryInterval_Ability.RandomInRange;
                job.checkOverrideOnExpire = true;
                return job;
            }
            else
            {
                Verb verb = pawn.TryGetAttackVerb(enemyTarget, flag, this.allowTurrets);
                if (verb == null)
                {
                    return null;
                }
                if (verb.verbProps.IsMeleeAttack)
                {
                    return this.MeleeAttackJob(pawn, enemyTarget);
                }
                IntVec3 enemyTargetPositionOnPawnMap;
                if (pawn.Map.Parent is MapParent_Vehicle parentVehicle)
                {
                    enemyTargetPositionOnPawnMap = IntVec3.Zero.OrigToVehicleMap(parentVehicle.vehicle) - enemyTargetPositionOnBaseMap;
                }
                else
                {
                    enemyTargetPositionOnPawnMap = enemyTargetPositionOnBaseMap;
                }
                bool flag2 = CoverUtility.CalculateOverallBlockChance(pawn, enemyTargetPositionOnPawnMap, pawn.Map) > 0.01f;
                bool flag3 = pawn.Position.Standable(pawn.Map) && pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted);
                bool flag4 = verb.CanHitTarget(enemyTarget);
                bool flag5 = (pawnPositionOnBaseMap - enemyTargetPositionOnBaseMap).LengthHorizontalSquared < 25;
                if ((flag2 && flag3 && flag4) || (flag5 && flag4))
                {
                    return JobMaker.MakeJob(JobDefOf.Wait_Combat, JobGiver_AIFightEnemy.ExpiryInterval_ShooterSucceeded.RandomInRange, true);
                }
                IntVec3 intVec2;
                if (!this.TryFindShootingPosition(pawn, out intVec2, null))
                {
                    return null;
                }
                if (intVec2 == pawn.Position)
                {
                    return JobMaker.MakeJob(JobDefOf.Wait_Combat, JobGiver_AIFightEnemy.ExpiryInterval_ShooterSucceeded.RandomInRange, true);
                }
                Job job2 = JobMaker.MakeJob(JobDefOf.Goto, intVec2);
                job2.expiryInterval = JobGiver_AIFightEnemy.ExpiryInterval_ShooterSucceeded.RandomInRange;
                job2.checkOverrideOnExpire = true;
                return job2;
            }
        }

        private Job GetAbilityJob(Pawn pawn, Thing enemyTarget)
        {
            if (pawn.abilities == null)
            {
                return null;
            }
            List<Ability> list = pawn.abilities.AICastableAbilities(enemyTarget, true);
            if (list.NullOrEmpty<Ability>())
            {
                return null;
            }
            if (pawn.Position.Standable(pawn.Map) && pawn.Map.pawnDestinationReservationManager.CanReserve(pawn.Position, pawn, pawn.Drafted))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].verb.CanHitTarget(enemyTarget))
                    {
                        return list[i].GetJob(enemyTarget, enemyTarget);
                    }
                }
                for (int j = 0; j < list.Count; j++)
                {
                    LocalTargetInfo localTargetInfo = list[j].AIGetAOETarget();
                    if (localTargetInfo.IsValid)
                    {
                        return list[j].GetJob(localTargetInfo, localTargetInfo);
                    }
                }
                for (int k = 0; k < list.Count; k++)
                {
                    if (list[k].verb.targetParams.canTargetSelf)
                    {
                        return list[k].GetJob(pawn, pawn);
                    }
                }
            }
            return null;
        }

        protected override void UpdateEnemyTarget(Pawn pawn)
        {
            Thing thing = pawn.mindState.enemyTarget;
            if (thing != null && this.ShouldLoseTarget(pawn))
            {
                thing = null;
            }
            if (thing == null)
            {
                thing = this.FindAttackTargetIfPossible(pawn);
                if (thing != null)
                {
                    pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
                    Lord lord = pawn.GetLord();
                    if (lord != null)
                    {
                        lord.Notify_PawnAcquiredTarget(pawn, thing);
                    }
                }
            }
            else
            {
                Thing thing2 = this.FindAttackTargetIfPossible(pawn);
                if (thing2 == null && !this.chaseTarget)
                {
                    thing = null;
                }
                else if (thing2 != null && thing2 != thing)
                {
                    pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
                    thing = thing2;
                }
            }
            pawn.mindState.enemyTarget = thing;
            Pawn pawn2;
            if ((pawn2 = (thing as Pawn)) != null && thing.Faction == Faction.OfPlayer && pawn.PositionOnBaseMap().InHorDistOf(thing.PositionOnBaseMap(), 40f) && !pawn2.IsShambler && !pawn.IsPsychologicallyInvisible())
            {
                Find.TickManager.slower.SignalForceNormalSpeed();
            }
        }

        protected override bool ShouldLoseTarget(Pawn pawn)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (!enemyTarget.Destroyed && Find.TickManager.TicksGame - pawn.mindState.lastEngageTargetTick <= this.TicksSinceEngageToLoseTarget &&
                pawn.CanReach(enemyTarget, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn) &&
                (float)(pawn.PositionOnBaseMap() - enemyTarget.PositionOnBaseMap()).LengthHorizontalSquared <= this.targetKeepRadius * this.targetKeepRadius)
            {
                IAttackTarget attackTarget = enemyTarget as IAttackTarget;
                return attackTarget != null && attackTarget.ThreatDisabled(pawn);
            }
            return true;
        }
    }
}
