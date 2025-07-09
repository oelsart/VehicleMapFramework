using RimWorld;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public class Verb_LaunchZipline : Verb_LaunchProjectile
{
    public Thing ZiplineEnd { get; set; }

    public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
    {
        var map = TargetMapManager.HasTargetMap(caster, out var map2) ? map2 : caster.Map;
        if (caster.Map == map)
        {
            Messages.Message("VMF_MustShotAtAnotherMap".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        return base.ValidateTarget(target, showMessages) && target.Cell.Standable(map);
    }

    public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
    {
        if (ZiplineEnd?.Spawned ?? false) return false;

        return base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
    }

    protected override bool TryCastShot()
    {
        ThingDef projectile = Projectile;
        if (projectile == null)
        {
            return false;
        }

        bool flag = this.TryFindShootLineFromToOnVehicle(caster.PositionOnBaseMap(), currentTarget, out ShootLine resultingLine);
        if (verbProps.stopBurstWithoutLos && !flag)
        {
            return false;
        }

        if (base.EquipmentSource != null)
        {
            base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
            base.EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
        }

        lastShotTick = Find.TickManager.TicksGame;
        Thing manningPawn = caster;
        Thing equipmentSource = base.EquipmentSource;
        CompMannable compMannable = caster.TryGetComp<CompMannable>();
        if (compMannable?.ManningPawn != null)
        {
            manningPawn = compMannable.ManningPawn;
            equipmentSource = caster;
        }

        Vector3 drawPos = caster.DrawPos;
        Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, resultingLine.Source, caster.BaseMap());
        ZiplineEnd = projectile2;
        if (projectile2 is Bullet_ZiplineEnd zipline)
        {
            zipline.launchVerb = this;
            if (TargetMapManager.HasTargetMap(caster, out var map))
            {
                zipline.destMap = map;
            }
        }
        if (verbProps.ForcedMissRadius > 0.5f)
        {
            float num = verbProps.ForcedMissRadius;
            if (manningPawn is Pawn pawn)
            {
                num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
            }

            float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, currentTarget.CellOnBaseMap() - caster.PositionOnBaseMap());
            if (num2 > 0.5f)
            {
                IntVec3 forcedMissTarget = GetForcedMissTarget(num2);
                if (forcedMissTarget != currentTarget.Cell)
                {
                    ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                    if (Rand.Chance(0.5f))
                    {
                        projectileHitFlags = ProjectileHitFlags.All;
                    }

                    if (!canHitNonTargetPawnsNow)
                    {
                        projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                    }

                    projectile2.Launch(manningPawn, drawPos, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
                    return true;
                }
            }
        }

        ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
        Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
        ThingDef targetCoverDef = randomCoverToMissInto?.def;
        if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
        {
            bool flyOverhead = projectile2?.def?.projectile != null && projectile2.def.projectile.flyOverhead;
            resultingLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget, flyOverhead, caster.BaseMap());
            ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
            if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
            {
                projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
            return true;
        }

        if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
        {
            ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
            if (canHitNonTargetPawnsNow)
            {
                projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
            return true;
        }

        ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
        if (canHitNonTargetPawnsNow)
        {
            projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
        }

        if (!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full)
        {
            projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
        }

        if (currentTarget.Thing != null)
        {
            projectile2.Launch(manningPawn, drawPos, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
        }
        else
        {
            projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
        }

        return true;
    }

    public override void DrawHighlight(LocalTargetInfo target)
    {
        if (caster != null && !caster.Spawned)
        {
            return;
        }
        var map = Patch_JumpUtility_OrderJump.TargetMap(caster);
        if (target.IsValid && JumpUtility.ValidJumpTarget(caster, map, target.Cell))
        {
            GenDraw.DrawTargetHighlightWithLayer(Patch_Verb_Jump_DrawHighlight.CenterVector3Offset(ref target, this), AltitudeLayer.MetaOverlays);
        }
        GenDraw.DrawRadiusRing(caster.Position, EffectiveRange, Color.white, c => GenSightOnVehicle.LineOfSight(caster.PositionOnBaseMap(), c, caster.BaseMap()) && JumpUtility.ValidJumpTarget(caster, caster.BaseMap(), c));
    }

    public override void OnGUI(LocalTargetInfo target)
    {
        if (CanHitTarget(target) && JumpUtility.ValidJumpTarget(caster, Patch_JumpUtility_OrderJump.TargetMap(caster), target.Cell))
        {
            base.OnGUI(target);
            return;
        }
        GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        var ziplineEnd = ZiplineEnd;
        Scribe_References.Look(ref ziplineEnd, "ZiplineEnd");
        ZiplineEnd = ziplineEnd;
    }
}
