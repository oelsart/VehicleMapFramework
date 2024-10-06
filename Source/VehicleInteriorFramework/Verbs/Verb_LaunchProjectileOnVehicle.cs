using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class Verb_LaunchProjectileOnVehicle : Verb
    {
        public virtual ThingDef Projectile
        {
            get
            {
                ThingWithComps equipmentSource = base.EquipmentSource;
                CompChangeableProjectile compChangeableProjectile = (equipmentSource != null) ? equipmentSource.GetComp<CompChangeableProjectile>() : null;
                if (compChangeableProjectile != null && compChangeableProjectile.Loaded)
                {
                    return compChangeableProjectile.Projectile;
                }
                return this.verbProps.defaultProjectile;
            }
        }

        public override void WarmupComplete()
        {
            base.WarmupComplete();
            BattleLog battleLog = Find.BattleLog;
            Thing caster = this.caster;
            Thing target = this.currentTarget.HasThing ? this.currentTarget.Thing : null;
            ThingWithComps equipmentSource = base.EquipmentSource;
            battleLog.Add(new BattleLogEntry_RangedFire(caster, target, (equipmentSource != null) ? equipmentSource.def : null, this.Projectile, this.ShotsPerBurst > 1));
        }

        protected IntVec3 GetForcedMissTarget(float forcedMissRadius)
        {
            var targCellOnBaseMap = this.currentTarget.CellOnBaseMap();
            if (this.verbProps.forcedMissEvenDispersal)
            {
                if (this.forcedMissTargetEvenDispersalCache.Count <= 0)
                {
                    this.forcedMissTargetEvenDispersalCache.AddRange(Verb_LaunchProjectileOnVehicle.GenerateEvenDispersalForcedMissTargets(targCellOnBaseMap, forcedMissRadius, this.burstShotsLeft));
                    this.forcedMissTargetEvenDispersalCache.SortByDescending((IntVec3 p) => p.DistanceToSquared(this.Caster.PositionOnBaseMap()));
                }
                if (this.forcedMissTargetEvenDispersalCache.Count > 0)
                {
                    return this.forcedMissTargetEvenDispersalCache.Pop<IntVec3>();
                }
            }
            int maxExclusive = GenRadial.NumCellsInRadius(forcedMissRadius);
            int num = Rand.Range(0, maxExclusive);
            return this.currentTarget.CellOnBaseMap() + GenRadial.RadialPattern[num];
        }

        private static IEnumerable<IntVec3> GenerateEvenDispersalForcedMissTargets(IntVec3 root, float radius, int count)
        {
            float randomRotationOffset = Rand.Range(0f, 360f);
            float goldenRatio = (1f + Mathf.Pow(5f, 0.5f)) / 2f;
            int num3;
            for (int i = 0; i < count; i = num3 + 1)
            {
                float f = 6.2831855f * (float)i / goldenRatio;
                float f2 = Mathf.Acos(1f - 2f * ((float)i + 0.5f) / (float)count);
                float num = (float)((int)(Mathf.Cos(f) * Mathf.Sin(f2) * radius));
                int num2 = (int)(Mathf.Cos(f2) * radius);
                Vector3 vect = new Vector3(num, 0f, (float)num2).RotatedBy(randomRotationOffset);
                yield return root + vect.ToIntVec3();
                num3 = i;
            }
            yield break;
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            if (this.caster == null)
            {
                Log.Error("Verb " + this.GetUniqueLoadID() + " needs caster to work (possibly lost during saving/loading).");
                return false;
            }
            if (!this.caster.Spawned)
            {
                return false;
            }
            if (this.state == VerbState.Bursting || !this.CanHitTarget(castTarg))
            {
                return false;
            }
            if (this.CausesTimeSlowdown(castTarg))
            {
                Find.TickManager.slower.SignalForceNormalSpeed();
            }
            this.surpriseAttack = surpriseAttack;
            this.canHitNonTargetPawnsNow = canHitNonTargetPawns;
            this.preventFriendlyFire = preventFriendlyFire;
            this.nonInterruptingSelfCast = nonInterruptingSelfCast;
            this.currentTarget = castTarg;
            this.currentDestination = destTarg;
            if (this.CasterIsPawn && this.verbProps.warmupTime > 0f)
            {
                ShootLine newShootLine;
                var casterPositionOnBaseMap = this.caster.PositionOnBaseMap();
                if (!this.TryFindShootLineFromToOnVehicle(casterPositionOnBaseMap, castTarg, out newShootLine, false))
                {
                    return false;
                }
                this.CasterPawn.Drawer.Notify_WarmingCastAlongLine(newShootLine, casterPositionOnBaseMap);
                float statValue = this.CasterPawn.GetStatValue(StatDefOf.AimingDelayFactor, true, -1);
                int ticks = (this.verbProps.warmupTime * statValue).SecondsToTicks();
                this.CasterPawn.stances.SetStance(new Stance_Warmup(ticks, castTarg, this));
                if (this.verbProps.stunTargetOnCastStart && castTarg.Pawn != null)
                {
                    castTarg.Pawn.stances.stunner.StunFor(ticks, null, false, true, false);
                }
            }
            else
            {
                Ability ability;
                if ((ability = (this.verbTracker.directOwner as Ability)) != null)
                {
                    ability.lastCastTick = Find.TickManager.TicksGame;
                }
                this.WarmupComplete();
            }
            return true;
        }

        protected override bool TryCastShot()
        {
            var targetBaseMap = this.currentTarget.HasThing ? this.currentTarget.Thing.BaseMapOfThing() : this.caster.BaseMapOfThing();
            var casterBaseMap = this.caster.BaseMapOfThing();
            var targCellOnBaseMap = this.currentTarget.CellOnBaseMap();
            var casterPositionOnBaseMap = this.caster.PositionOnBaseMap();
            if (this.currentTarget.HasThing && targetBaseMap != casterBaseMap)
            {
                return false;
            }
            ThingDef projectile = this.Projectile;
            if (projectile == null)
            {
                return false;
            }
            ShootLine shootLine;
            bool flag = this.TryFindShootLineFromToOnVehicle(casterPositionOnBaseMap, this.currentTarget, out shootLine, false);
            if (this.verbProps.stopBurstWithoutLos && !flag)
            {
                return false;
            }
            if (base.EquipmentSource != null)
            {
                CompChangeableProjectile comp = base.EquipmentSource.GetComp<CompChangeableProjectile>();
                if (comp != null)
                {
                    comp.Notify_ProjectileLaunched();
                }
                CompApparelVerbOwner_Charged comp2 = base.EquipmentSource.GetComp<CompApparelVerbOwner_Charged>();
                if (comp2 != null)
                {
                    comp2.UsedOnce();
                }
            }
            this.lastShotTick = Find.TickManager.TicksGame;
            Thing thing = this.caster;
            Thing equipment = base.EquipmentSource;
            CompMannable compMannable = this.caster.TryGetComp<CompMannable>();
            if (((compMannable != null) ? compMannable.ManningPawn : null) != null)
            {
                thing = compMannable.ManningPawn;
                equipment = this.caster;
            }
            Vector3 drawPos = this.caster.DrawPos;
            Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, shootLine.Source, casterBaseMap, WipeMode.Vanish);
            if (this.verbProps.ForcedMissRadius > 0.5f)
            {
                float num = this.verbProps.ForcedMissRadius;
                Pawn caster;
                if ((caster = (thing as Pawn)) != null)
                {
                    num *= this.verbProps.GetForceMissFactorFor(equipment, caster);
                }
                float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, targCellOnBaseMap - casterPositionOnBaseMap);
                if (num2 > 0.5f)
                {
                    IntVec3 forcedMissTarget = this.GetForcedMissTarget(num2);
                    if (forcedMissTarget != targCellOnBaseMap)
                    {
                        this.ThrowDebugText("ToRadius");
                        this.ThrowDebugText("Rad\nDest", forcedMissTarget);
                        ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                        if (Rand.Chance(0.5f))
                        {
                            projectileHitFlags = ProjectileHitFlags.All;
                        }
                        if (!this.canHitNonTargetPawnsNow)
                        {
                            projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                        }
                        projectile2.Launch(thing, drawPos, forcedMissTarget, this.currentTarget, projectileHitFlags, this.preventFriendlyFire, equipment, null);
                        return true;
                    }
                }
            }
            ShotReportOnVehicle ShotReportOnVehicle = ShotReportOnVehicle.HitReportFor(this.caster, this, this.currentTarget);
            Thing randomCoverToMissInto = ShotReportOnVehicle.GetRandomCoverToMissInto();
            ThingDef targetCoverDef = (randomCoverToMissInto != null) ? randomCoverToMissInto.def : null;
            if (this.verbProps.canGoWild && !Rand.Chance(ShotReportOnVehicle.AimOnTargetChance_IgnoringPosture))
            {
                bool flag2;
                if (projectile2 == null)
                {
                    flag2 = (null != null);
                }
                else
                {
                    ThingDef def = projectile2.def;
                    flag2 = (((def != null) ? def.projectile : null) != null);
                }
                bool flyOverhead = flag2 && projectile2.def.projectile.flyOverhead;
                shootLine.ChangeDestToMissWild_NewTemp(ShotReportOnVehicle.AimOnTargetChance_StandardTarget, flyOverhead, casterBaseMap);
                this.ThrowDebugText("ToWild" + (this.canHitNonTargetPawnsNow ? "\nchntp" : ""));
                this.ThrowDebugText("Wild\nDest", shootLine.Dest);
                ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
                if (Rand.Chance(0.5f) && this.canHitNonTargetPawnsNow)
                {
                    //projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
                }
                projectile2.Launch(thing, drawPos, shootLine.Dest, this.currentTarget, projectileHitFlags2, this.preventFriendlyFire, equipment, targetCoverDef);
                return true;
            }
            if (this.currentTarget.Thing != null && this.currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(ShotReportOnVehicle.PassCoverChance))
            {
                this.ThrowDebugText("ToCover" + (this.canHitNonTargetPawnsNow ? "\nchntp" : ""));
                this.ThrowDebugText("Cover\nDest", randomCoverToMissInto.Position);
                ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
                if (this.canHitNonTargetPawnsNow)
                {
                    projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
                }
                projectile2.Launch(thing, drawPos, randomCoverToMissInto, this.currentTarget, projectileHitFlags3, this.preventFriendlyFire, equipment, targetCoverDef);
                return true;
            }
            ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
            if (this.canHitNonTargetPawnsNow)
            {
                projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
            }
            if (!this.currentTarget.HasThing || this.currentTarget.Thing.def.Fillage == FillCategory.Full)
            {
                projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
            }
            this.ThrowDebugText("ToHit" + (this.canHitNonTargetPawnsNow ? "\nchntp" : ""));
            if (this.currentTarget.Thing != null)
            {
                projectile2.Launch(thing, drawPos, this.currentTarget, this.currentTarget, projectileHitFlags4, this.preventFriendlyFire, equipment, targetCoverDef);
                this.ThrowDebugText("Hit\nDest", this.currentTarget.CellOnBaseMap());
            }
            else
            {
                projectile2.Launch(thing, drawPos, shootLine.Dest, this.currentTarget, projectileHitFlags4, this.preventFriendlyFire, equipment, targetCoverDef);
                this.ThrowDebugText("Hit\nDest", shootLine.Dest);
            }
            return true;
        }

        private void ThrowDebugText(string text)
        {
            if (DebugViewSettings.drawShooting)
            {
                MoteMaker.ThrowText(this.caster.DrawPos, this.caster.Map, text, -1f);
            }
        }

        private void ThrowDebugText(string text, IntVec3 c)
        {
            if (DebugViewSettings.drawShooting)
            {
                MoteMaker.ThrowText(c.ToVector3Shifted(), this.caster.Map, text, -1f);
            }
        }

        public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
        {
            needLOSToCenter = true;
            ThingDef projectile = this.Projectile;
            if (projectile == null)
            {
                return 0f;
            }
            float num = projectile.projectile.explosionRadius + projectile.projectile.explosionRadiusDisplayPadding;
            float forcedMissRadius = this.verbProps.ForcedMissRadius;
            if (forcedMissRadius > 0f && this.verbProps.burstShotCount > 1)
            {
                num += forcedMissRadius;
            }
            return num;
        }

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }
            if (this.CasterIsPawn)
            {
                Pawn casterPawn = this.CasterPawn;
                if (casterPawn.Faction != Faction.OfPlayer && !this.verbProps.ai_ProjectileLaunchingIgnoresMeleeThreats && casterPawn.mindState.MeleeThreatStillThreat && casterPawn.mindState.meleeThreat.Position.AdjacentTo8WayOrInside(casterPawn.Position))
                {
                    return false;
                }
            }
            return this.Projectile != null;
        }

        public override void Reset()
        {
            base.Reset();
            this.forcedMissTargetEvenDispersalCache.Clear();
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return this.caster != null && this.caster.Spawned && (targ == this.caster || this.CanHitTargetFrom(this.caster.PositionOnBaseMap(), targ));
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            if (targ.Thing != null && targ.Thing == this.caster)
            {
                return this.targetParams.canTargetSelf;
            }
            ShootLine shootLine;
            return (targ.Pawn == null || !targ.Pawn.IsPsychologicallyInvisible() || !this.caster.HostileTo(targ.Pawn)) && !this.ApparelPreventsShooting() && this.TryFindShootLineFromToOnVehicle(root, targ, out shootLine, false);
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            {
                if (this.verbProps.IsMeleeAttack)
                {
                    /*
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                    job.playerForced = true;
                    Pawn pawn = target.Thing as Pawn;
                    if (pawn != null)
                    {
                        job.killIncappedTarget = pawn.Downed;
                    }
                    this.CasterPawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                    */
                    return;
                }
                float num = this.verbProps.EffectiveMinRange(target, this.CasterPawn);
                var casterPositionOnBaseMap = this.CasterPawn.PositionOnBaseMap();
                var targetCellOnBaseMap = target.CellOnBaseMap();
                if ((float)casterPositionOnBaseMap.DistanceToSquared(targetCellOnBaseMap) < num * num && casterPositionOnBaseMap.AdjacentTo8WayOrInside(targetCellOnBaseMap))
                {
                    Messages.Message("MessageCantShootInMelee".Translate(), this.CasterPawn, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                Job job2 = JobMaker.MakeJob(this.verbProps.ai_IsWeapon ? JobDefOf.AttackStatic : JobDefOf.UseVerbOnThing);
                job2.verbToUse = this;
                job2.targetA = target;
                job2.endIfCantShootInMelee = true;
                this.CasterPawn.jobs.TryTakeOrderedJob(job2, new JobTag?(JobTag.Misc), false);
            }
        }

        private List<IntVec3> forcedMissTargetEvenDispersalCache = new List<IntVec3>();

        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();

        private static List<IntVec3> tempDestList = new List<IntVec3>();
    }
}
