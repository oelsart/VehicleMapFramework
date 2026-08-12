using RimWorld;
using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public class Verb_LaunchZipline : Verb_LaunchProjectile, IAbilityVerb
{
    public Thing ziplineEnd;

    private Ability ability;
    
    public Ability Ability
    {
        get => ability;
        set => ability = value;
    }

    public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
    {
        var map = target.Thing?.Map ?? caster.TargetMap ?? (caster as Pawn)?.mindState?.enemyTarget?.Map ?? caster.Map;
        if (Ability is null && caster.Map == map)
        {
            if (showMessages)
                Messages.Message("VMF_MustShotAtAnotherMap".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        return base.ValidateTarget(target, showMessages) && target.Cell.Walkable(map);
    }

    public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
    {
        return targ.Thing switch
        {
            { } t when t == caster => targetParams.canTargetSelf,
            _ => (targ.Pawn == null || !targ.Pawn.IsPsychologicallyInvisible() || !caster.HostileTo(targ.Pawn)) &&
                 !ApparelPreventsShooting() && this.TryFindShootLineFromToOnVehicle(root, targ, out _)
        };
    }

    protected override bool TryCastShot()
    {
        var projectile = Projectile;
        if (projectile == null)
        {
            return false;
        }
        
        var target = currentTarget;
        var destMap = target.Thing?.Map ?? caster.TargetMap ?? (caster as Pawn)?.mindState?.enemyTarget?.Map ?? caster.Map;
        var flag = this.TryFindShootLineFromToOnVehicle(caster.PositionOnBaseMap, target, out var resultingLine);
        if (verbProps.stopBurstWithoutLos && !flag)
        {
            return false;
        }

        if (EquipmentSource != null)
        {
            EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
            EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
        }

        lastShotTick = Find.TickManager.TicksGame;
        var manningPawn = caster;
        Thing equipmentSource = EquipmentSource;
        var compMannable = caster.TryGetComp<CompMannable>();
        if (compMannable?.ManningPawn != null)
        {
            manningPawn = compMannable.ManningPawn;
            equipmentSource = caster;
        }

        var drawPos = caster.DrawPos;
        var offset = caster.def.building?.turretTopOffset.ToVector3() ?? Vector3.zero;
        if (caster.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            offset = offset.RotatedBy(-vehicle.Angle + vehicle.Transform.rotation);
        }
        drawPos += offset;
        
        var projectile2 = (Projectile)ThingMaker.MakeThing(projectile);
        if (projectile2 is Bullet_ZiplineEnd zipline)
        {
            zipline.launchVerb = this;
            zipline.destMap = destMap;

            if (zipline.def.GetModExtension<CustomZipline>() is { } customZipline)
            {
                zipline.ZipLineData = customZipline.zipLineData;
            }
        }
        GenSpawn.Spawn(projectile2, resultingLine.Source, caster.GroundMap);
        if (verbProps.ForcedMissRadius > 0.5f)
        {
            var num = verbProps.ForcedMissRadius;
            if (manningPawn is Pawn pawn)
            {
                num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
            }

            var num2 = VerbUtility.CalculateAdjustedForcedMiss(num, target.Cell.ToBaseMapCoord(destMap) - caster.PositionOnBaseMap);
            if (num2 > 0.5f)
            {
                var forcedMissTarget = GetForcedMissTarget(num2);
                if (forcedMissTarget != target.Cell)
                {
                    var projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                    if (Rand.Chance(0.5f))
                    {
                        projectileHitFlags = ProjectileHitFlags.All;
                    }

                    if (!canHitNonTargetPawnsNow)
                    {
                        projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                    }

                    projectile2.Launch(manningPawn, drawPos, forcedMissTarget, target, projectileHitFlags, preventFriendlyFire, equipmentSource);
                    return true;
                }
            }
        }

        var shotReport = ShotReport.HitReportFor(caster, this, target);
        var randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
        var targetCoverDef = randomCoverToMissInto?.def;
        if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
        {
            var flyOverhead = projectile2?.def?.projectile is { flyOverhead: true };
            resultingLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget, flyOverhead, caster.BaseMap());
            var projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
            if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
            {
                projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, target, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
            return true;
        }

        if (target.Thing != null && target.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
        {
            var projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
            if (canHitNonTargetPawnsNow)
            {
                projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto, target, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
            return true;
        }

        projectile2.Launch(manningPawn, drawPos, target.Thing != null ? target : resultingLine.Dest,
            target, ProjectileHitFlags.IntendedTarget, preventFriendlyFire, equipmentSource, targetCoverDef);
        return true;
    }

    public override void DrawHighlight(LocalTargetInfo target)
    {
        if (caster is { Spawned: false })
        {
            return;
        }
        var map = caster.TargetMapOrThingMap;
        if (target.IsValid && JumpUtility.ValidJumpTarget(caster, map, target.Cell))
        {
            GenDraw.DrawTargetHighlightWithLayer(Patch_Verb_Jump_DrawHighlight.CenterVector3Offset(ref target, this), AltitudeLayer.MetaOverlays);
        }

        var baseMap = caster.GroundMap;
        GenDraw.DrawRadiusRing(caster.Position, EffectiveRange, Color.white,
            c =>
                GenSightOnVehicle.LineOfSight(caster.PositionOnBaseMap, c, baseMap, false) &&
                (JumpUtility.ValidJumpTarget(caster, baseMap, c) ||
                 c.InBounds(baseMap) && baseMap.GetCachedMapComponent<VehicleMapGrid>().VehicleAt(c) is { } vehicle &&
                 JumpUtility.ValidJumpTarget(caster, vehicle.VehicleMap, c.ToVehicleMapCoord(vehicle))));
    }

    public override void OnGUI(LocalTargetInfo target)
    {
        if (!target.IsValid) return;
        if (CanHitTarget(target) && JumpUtility.ValidJumpTarget(caster, caster.TargetMapOrThingMap, target.Cell))
        {
            base.OnGUI(target);
            return;
        }
        GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref ability, "ability");       
    }
}
