using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors
{
    public class JobGiver_AIFightEnemiesOnVehicle : JobGiver_AIFightEnemy
    {
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AIFightEnemiesOnVehicle jobGiver_AIFightEnemiesOnVehicle = (JobGiver_AIFightEnemiesOnVehicle)base.DeepCopy(resolve);
            jobGiver_AIFightEnemiesOnVehicle.needLOSToAcquireNonPawnTargets = this.needLOSToAcquireNonPawnTargets;
            jobGiver_AIFightEnemiesOnVehicle.dest1 = this.dest1;
            jobGiver_AIFightEnemiesOnVehicle.dest2 = this.dest2;
            return jobGiver_AIFightEnemiesOnVehicle;
        }

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
            if (pawn.Map.dangerWatcher.DangerRating == StoryDanger.None) return null;

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
                    return JobAcrossMapsUtility.GotoDestMapJob(pawn, this.dest1, this.dest2, JobMaker.MakeJob(JobDefOf.AttackMelee, enemyTarget));
                }
                bool flag2 = CoverUtility.CalculateOverallBlockChance(pawn, enemyTarget.PositionOnAnotherThingMap(pawn), pawn.Map) > 0.01f;
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

        protected override Thing FindAttackTarget(Pawn pawn)
        {
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedReachableIfCantHitFromMyPos | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (this.needLOSToAcquireNonPawnTargets)
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToNonPawns;
            }
            if (this.PrimaryVerbIsIncendiary(pawn))
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }
            if (this.ignoreNonCombatants)
            {
                targetScanFlags |= TargetScanFlags.IgnoreNonCombatants;
            }
            return (Thing)AttackTargetFinder.BestAttackTarget(pawn, targetScanFlags, (Thing x) => this.ExtraTargetValidator(pawn, x), 0f, this.targetAcquireRadius, this.GetFlagPosition(pawn), this.GetFlagRadius(pawn), false, true, false, this.OnlyUseRangedSearch);
        }

        private bool PrimaryVerbIsIncendiary(Pawn pawn)
        {
            Pawn_EquipmentTracker equipment = pawn.equipment;
            if (((equipment != null) ? equipment.Primary : null) != null)
            {
                List<Verb> allVerbs = pawn.equipment.Primary.GetComp<CompEquippable>().AllVerbs;
                for (int i = 0; i < allVerbs.Count; i++)
                {
                    if (allVerbs[i].verbProps.isPrimary)
                    {
                        return allVerbs[i].IsIncendiary_Ranged();
                    }
                }
            }
            return false;
        }

        protected override bool ShouldLoseTarget(Pawn pawn)
        {
            this.dest1 = TargetInfo.Invalid;
            this.dest2 = TargetInfo.Invalid;
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (!enemyTarget.Destroyed && Find.TickManager.TicksGame - pawn.mindState.lastEngageTargetTick <= this.TicksSinceEngageToLoseTarget &&
                pawn.CanReach(enemyTarget, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, enemyTarget.Map, out this.dest1, out this.dest2) &&
                (float)(pawn.PositionOnBaseMap() - enemyTarget.PositionOnBaseMap()).LengthHorizontalSquared <= this.targetKeepRadius * this.targetKeepRadius)
            {
                IAttackTarget attackTarget = enemyTarget as IAttackTarget;
                return attackTarget != null && attackTarget.ThreatDisabled(pawn);
            }
            return true;
        }

        private bool needLOSToAcquireNonPawnTargets;

        private TargetInfo dest1;

        private TargetInfo dest2;
    }
}
