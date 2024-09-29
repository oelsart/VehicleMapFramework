using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.Sound;
using Verse;

namespace VehicleInteriors.Verbs
{
    public class Verb_ShootBeamOnVehicle : Verb
    {
        protected override int ShotsPerBurst
        {
            get
            {
                return this.verbProps.burstShotCount;
            }
        }

        public float ShotProgress
        {
            get
            {
                return (float)this.ticksToNextPathStep / (float)this.verbProps.ticksBetweenBurstShots;
            }
        }

        public Vector3 InterpolatedPosition
        {
            get
            {
                Vector3 b = base.CurrentTarget.CenterVector3 - this.initialTargetPosition;
                return Vector3.Lerp(this.path[this.burstShotsLeft], this.path[Mathf.Min(this.burstShotsLeft + 1, this.path.Count - 1)], this.ShotProgress) + b;
            }
        }

        public override float? AimAngleOverride
        {
            get
            {
                if (this.state != VerbState.Bursting)
                {
                    return null;
                }
                return new float?((this.InterpolatedPosition - this.caster.DrawPos).AngleFlat());
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            var casterPositionOnBaseMap = this.caster.PositionOnBaseMap();
            var targCellOnBaseMap = target.CellOnBaseMap();
            this.verbProps.DrawRadiusRing(casterPositionOnBaseMap);
            if (target.IsValid)
            {
                if (target.Thing != null)
                {
                    Vector3 vector = target.Thing.DrawPos;
                    Graphics.DrawMesh(MeshPool.plane10, new Vector3(vector.x, AltitudeLayer.MapDataOverlay.AltitudeFor(), vector.z), target.Thing.Rotation.AsQuat, GenDraw.CurTargetingMat, 0);
                    if (target.Thing is Pawn || target.Thing is Corpse)
                    {
                        TargetHighlighter.Highlight(target.Thing, false, true, false);
                    }
                    return;
                }
                GenDraw.DrawTargetHighlightWithLayer(targCellOnBaseMap, AltitudeLayer.Building, null);
                this.DrawHighlightFieldRadiusAroundTarget(target);
            }
            this.CalculatePath(target.CenterVector3, this.tmpPath, this.tmpPathCells, false);
            foreach (IntVec3 targetCell in this.tmpPathCells)
            {
                ShootLine shootLine;
                bool flag = this.TryFindShootLineFromToOnVehicle(casterPositionOnBaseMap, target, out shootLine, false);
                IntVec3 intVec;
                if ((!this.verbProps.stopBurstWithoutLos || flag) && this.TryGetHitCell(shootLine.Source, targetCell, out intVec))
                {
                    this.tmpHighlightCells.Add(intVec);
                    if (this.verbProps.beamHitsNeighborCells)
                    {
                        foreach (IntVec3 item in this.GetBeamHitNeighbourCells(shootLine.Source, intVec))
                        {
                            if (!this.tmpHighlightCells.Contains(item))
                            {
                                this.tmpSecondaryHighlightCells.Add(item);
                            }
                        }
                    }
                }
            }
            this.tmpSecondaryHighlightCells.RemoveWhere((IntVec3 x) => this.tmpHighlightCells.Contains(x));
            if (this.tmpHighlightCells.Any<IntVec3>())
            {
                GenDraw.DrawFieldEdges(this.tmpHighlightCells.ToList<IntVec3>(), this.verbProps.highlightColor ?? Color.white, null);
            }
            if (this.tmpSecondaryHighlightCells.Any<IntVec3>())
            {
                GenDraw.DrawFieldEdges(this.tmpSecondaryHighlightCells.ToList<IntVec3>(), this.verbProps.secondaryHighlightColor ?? Color.white, null);
            }
            this.tmpHighlightCells.Clear();
            this.tmpSecondaryHighlightCells.Clear();
        }

        protected override bool TryCastShot()
        {
            if (this.currentTarget.HasThing && this.currentTarget.Thing.BaseMapOfThing() != this.caster.BaseMapOfThing())
            {
                return false;
            }
            ShootLine shootLine;
            bool flag = this.TryFindShootLineFromToOnVehicle(this.caster.PositionOnBaseMap(), this.currentTarget, out shootLine, false);
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
                CompApparelReloadable comp2 = base.EquipmentSource.GetComp<CompApparelReloadable>();
                if (comp2 != null)
                {
                    comp2.UsedOnce();
                }
            }
            this.lastShotTick = Find.TickManager.TicksGame;
            this.ticksToNextPathStep = this.verbProps.ticksBetweenBurstShots;
            IntVec3 targetCell = this.InterpolatedPosition.Yto0().ToIntVec3();
            IntVec3 intVec;
            if (!this.TryGetHitCell(shootLine.Source, targetCell, out intVec))
            {
                return true;
            }
            this.HitCell(intVec, shootLine.Source, 1f);
            if (this.verbProps.beamHitsNeighborCells)
            {
                this.hitCells.Add(intVec);
                foreach (IntVec3 intVec2 in this.GetBeamHitNeighbourCells(shootLine.Source, intVec))
                {
                    if (!this.hitCells.Contains(intVec2))
                    {
                        float damageFactor = this.pathCells.Contains(intVec2) ? 1f : 0.5f;
                        this.HitCell(intVec2, shootLine.Source, damageFactor);
                        this.hitCells.Add(intVec2);
                    }
                }
            }
            return true;
        }

        protected bool TryGetHitCell(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell)
        {
            IntVec3 intVec = GenSight.LastPointOnLineOfSight(source, targetCell, (IntVec3 c) => c.InBounds(this.caster.Map) && c.CanBeSeenOverFast(this.caster.Map), true);
            if (this.verbProps.beamCantHitWithinMinRange && intVec.DistanceTo(source) < this.verbProps.minRange)
            {
                hitCell = default(IntVec3);
                return false;
            }
            hitCell = (intVec.IsValid ? intVec : targetCell);
            return intVec.IsValid;
        }

        protected IntVec3 GetHitCell(IntVec3 source, IntVec3 targetCell)
        {
            IntVec3 result;
            this.TryGetHitCell(source, targetCell, out result);
            return result;
        }

        protected IEnumerable<IntVec3> GetBeamHitNeighbourCells(IntVec3 source, IntVec3 pos)
        {
            if (!this.verbProps.beamHitsNeighborCells)
            {
                yield break;
            }
            int num;
            for (int i = 0; i < 4; i = num + 1)
            {
                IntVec3 intVec = pos + GenAdj.CardinalDirections[i];
                if (intVec.InBounds(this.Caster.Map) && (!this.verbProps.beamHitsNeighborCellsRequiresLOS || GenSight.LineOfSight(source, intVec, this.caster.Map)))
                {
                    yield return intVec;
                }
                num = i;
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
            var casterPositionOnBaseMap = this.caster.PositionOnBaseMap();
            if (this.CasterIsPawn && this.verbProps.warmupTime > 0f)
            {
                ShootLine newShootLine;
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

        public override void BurstingTick()
        {
            var casterPositionOnBaseMap = this.caster.PositionOnBaseMap();
            var casterBaseMap = this.caster.BaseMapOfThing();

            this.ticksToNextPathStep--;
            Vector3 vector = this.InterpolatedPosition;
            IntVec3 intVec = vector.ToIntVec3();
            Vector3 vector2 = this.InterpolatedPosition - casterPositionOnBaseMap.ToVector3Shifted();
            float num = vector2.MagnitudeHorizontal();
            Vector3 normalized = vector2.Yto0().normalized;
            IntVec3 b = GenSight.LastPointOnLineOfSight(casterPositionOnBaseMap, intVec, (IntVec3 c) => c.CanBeSeenOverFast(casterBaseMap), true);
            if (b.IsValid)
            {
                num -= (intVec - b).LengthHorizontal;
                vector = casterPositionOnBaseMap.ToVector3Shifted() + normalized * num;
                intVec = vector.ToIntVec3();
            }
            Vector3 offsetA = normalized * this.verbProps.beamStartOffset;
            Vector3 vector3 = vector - intVec.ToVector3Shifted();
            if (this.mote != null)
            {
                this.mote.UpdateTargets(new TargetInfo(casterPositionOnBaseMap, casterBaseMap, false), new TargetInfo(intVec, casterBaseMap, false), offsetA, vector3);
                this.mote.Maintain();
            }
            if (this.verbProps.beamGroundFleckDef != null && Rand.Chance(this.verbProps.beamFleckChancePerTick))
            {
                FleckMaker.Static(vector, casterBaseMap, this.verbProps.beamGroundFleckDef, 1f);
            }
            if (this.endEffecter == null && this.verbProps.beamEndEffecterDef != null)
            {
                this.endEffecter = this.verbProps.beamEndEffecterDef.Spawn(intVec, casterBaseMap, vector3, 1f);
            }
            if (this.endEffecter != null)
            {
                this.endEffecter.offset = vector3;
                this.endEffecter.EffectTick(new TargetInfo(intVec, casterBaseMap, false), TargetInfo.Invalid);
                this.endEffecter.ticksLeft--;
            }
            if (this.verbProps.beamLineFleckDef != null)
            {
                float num2 = 1f * num;
                int num3 = 0;
                while ((float)num3 < num2)
                {
                    if (Rand.Chance(this.verbProps.beamLineFleckChanceCurve.Evaluate((float)num3 / num2)))
                    {
                        Vector3 b2 = (float)num3 * normalized - normalized * Rand.Value + normalized / 2f;
                        FleckMaker.Static(casterPositionOnBaseMap.ToVector3Shifted() + b2, casterBaseMap, this.verbProps.beamLineFleckDef, 1f);
                    }
                    num3++;
                }
            }
            Sustainer sustainer = this.sustainer;
            if (sustainer == null)
            {
                return;
            }
            sustainer.Maintain();
        }

        public override void WarmupComplete()
        {
            var casterBaseMap = this.caster.BaseMapOfThing();

            this.burstShotsLeft = this.ShotsPerBurst;
            this.state = VerbState.Bursting;
            this.initialTargetPosition = this.currentTarget.CenterVector3;
            this.CalculatePath(this.currentTarget.CenterVector3, this.path, this.pathCells, true);
            this.hitCells.Clear();
            if (this.verbProps.beamMoteDef != null)
            {
                this.mote = MoteMaker.MakeInteractionOverlay(this.verbProps.beamMoteDef, this.caster, new TargetInfo(this.path[0].ToIntVec3(), casterBaseMap, false));
            }
            base.TryCastNextBurstShot();
            this.ticksToNextPathStep = this.verbProps.ticksBetweenBurstShots;
            Effecter effecter = this.endEffecter;
            if (effecter != null)
            {
                effecter.Cleanup();
            }
            if (this.verbProps.soundCastBeam != null)
            {
                this.sustainer = this.verbProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap(this.caster, MaintenanceType.PerTick));
            }
        }

        private void CalculatePath(Vector3 target, List<Vector3> pathList, HashSet<IntVec3> pathCellsList, bool addRandomOffset = true)
        {
            pathList.Clear();
            Vector3 vector = (target - this.caster.PositionOnBaseMap().ToVector3Shifted()).Yto0();
            float magnitude = vector.magnitude;
            Vector3 normalized = vector.normalized;
            Vector3 a = normalized.RotatedBy(-90f);
            float num = (this.verbProps.beamFullWidthRange > 0f) ? Mathf.Min(magnitude / this.verbProps.beamFullWidthRange, 1f) : 1f;
            float d = (this.verbProps.beamWidth + 1f) * num / (float)this.ShotsPerBurst;
            Vector3 vector2 = target.Yto0() - a * this.verbProps.beamWidth / 2f * num;
            pathList.Add(vector2);
            for (int i = 0; i < this.ShotsPerBurst; i++)
            {
                Vector3 a2 = normalized * (Rand.Value * this.verbProps.beamMaxDeviation) - normalized / 2f;
                Vector3 vector3 = Mathf.Sin(((float)i / (float)this.ShotsPerBurst + 0.5f) * 3.1415927f * 57.29578f) * this.verbProps.beamCurvature * -normalized - normalized * this.verbProps.beamMaxDeviation / 2f;
                if (addRandomOffset)
                {
                    pathList.Add(vector2 + (a2 + vector3) * num);
                }
                else
                {
                    pathList.Add(vector2 + vector3 * num);
                }
                vector2 += a * d;
            }
            pathCellsList.Clear();
            foreach (Vector3 vect in pathList)
            {
                pathCellsList.Add(vect.ToIntVec3());
            }
        }

        private bool CanHit(Thing thing)
        {
            return thing.Spawned && !CoverUtility.ThingCovered(thing, thing.Map);
        }

        private void HitCell(IntVec3 cell, IntVec3 sourceCell, float damageFactor = 1f)
        {
            if (!cell.InBounds(this.caster.BaseMapOfThing()))
            {
                return;
            }
            this.ApplyDamage(VerbUtility.ThingsToHit(cell, this.caster.BaseMapOfThing(), new Func<Thing, bool>(this.CanHit)).RandomElementWithFallback(null), sourceCell, damageFactor);
            if (this.verbProps.beamSetsGroundOnFire && Rand.Chance(this.verbProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(cell, this.caster.BaseMapOfThing(), 1f, this.caster, null);
            }
        }

        private void ApplyDamage(Thing thing, IntVec3 sourceCell, float damageFactor = 1f)
        {
            IntVec3 intVec = this.InterpolatedPosition.Yto0().ToIntVec3();
            IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(sourceCell, intVec, (IntVec3 c) => c.InBounds(this.caster.Map) && c.CanBeSeenOverFast(this.caster.Map), true);
            if (intVec2.IsValid)
            {
                intVec = intVec2;
            }
            Map map = this.caster.BaseMapOfThing();
            if (thing != null && this.verbProps.beamDamageDef != null)
            {
                float angleFlat = (this.currentTarget.CellOnBaseMap() - this.caster.PositionOnBaseMap()).AngleFlat;
                BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(this.caster, thing, this.currentTarget.Thing, base.EquipmentSource.def, null, null);
                DamageInfo dinfo;
                if (this.verbProps.beamTotalDamage > 0f)
                {
                    float num = this.verbProps.beamTotalDamage / (float)this.pathCells.Count;
                    num *= damageFactor;
                    dinfo = new DamageInfo(this.verbProps.beamDamageDef, num, this.verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, this.caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, this.currentTarget.Thing, true, true, QualityCategory.Normal, true);
                }
                else
                {
                    float amount = (float)this.verbProps.beamDamageDef.defaultDamage * damageFactor;
                    dinfo = new DamageInfo(this.verbProps.beamDamageDef, amount, this.verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, this.caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, this.currentTarget.Thing, true, true, QualityCategory.Normal, true);
                }
                thing.TakeDamage(dinfo).AssociateWithLog(log);
                if (thing.CanEverAttachFire())
                {
                    float chance;
                    if (this.verbProps.flammabilityAttachFireChanceCurve != null)
                    {
                        chance = this.verbProps.flammabilityAttachFireChanceCurve.Evaluate(thing.GetStatValue(StatDefOf.Flammability, true, -1));
                    }
                    else
                    {
                        chance = this.verbProps.beamChanceToAttachFire;
                    }
                    if (Rand.Chance(chance))
                    {
                        thing.TryAttachFire(this.verbProps.beamFireSizeRange.RandomInRange, this.caster);
                        return;
                    }
                }
                else if (Rand.Chance(this.verbProps.beamChanceToStartFire))
                {
                    FireUtility.TryStartFireIn(intVec, map, this.verbProps.beamFireSizeRange.RandomInRange, this.caster, this.verbProps.flammabilityAttachFireChanceCurve);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<Vector3>(ref this.path, "path", LookMode.Value, Array.Empty<object>());
            Scribe_Values.Look<int>(ref this.ticksToNextPathStep, "ticksToNextPathStep", 0, false);
            Scribe_Values.Look<Vector3>(ref this.initialTargetPosition, "initialTargetPosition", default(Vector3), false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && this.path == null)
            {
                this.path = new List<Vector3>();
            }
        }

        private List<Vector3> path = new List<Vector3>();

        private List<Vector3> tmpPath = new List<Vector3>();

        private int ticksToNextPathStep;

        private Vector3 initialTargetPosition;

        private MoteDualAttached mote;

        private Effecter endEffecter;

        private Sustainer sustainer;

        private HashSet<IntVec3> pathCells = new HashSet<IntVec3>();

        private HashSet<IntVec3> tmpPathCells = new HashSet<IntVec3>();

        private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();

        private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();

        private HashSet<IntVec3> hitCells = new HashSet<IntVec3>();

        private const int NumSubdivisionsPerUnitLength = 1;
    }
}
