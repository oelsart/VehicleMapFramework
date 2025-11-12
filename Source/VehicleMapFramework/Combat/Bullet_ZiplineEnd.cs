using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class Bullet_ZiplineEnd : Bullet, IZiplineEnd
{
    private float ArcHeightFactor
    {
        get
        {
            var num = def.projectile.arcHeightFactor;
            var num2 = (destination - origin).MagnitudeHorizontalSquared();
            if (num * num > num2 * 0.2f * 0.2f)
            {
                num = Mathf.Sqrt(num2) * 0.2f;
            }
            return num;
        }
    }

    public override void Launch(Thing launcher_, Vector3 origin_, LocalTargetInfo usedTarget_, LocalTargetInfo intendedTarget_, ProjectileHitFlags hitFlags, bool preventFriendlyFire_ = false, Thing equipment_ = null, ThingDef targetCoverDef_ = null)
    {
        base.Launch(launcher_, origin_, usedTarget_, intendedTarget_, hitFlags, preventFriendlyFire_, equipment_, targetCoverDef_);
        this.origin += (Vector3.forward * ZiplineEnd.LauncherOffset).RotatedBy(ExactRotation.eulerAngles.y);
    }
    protected override void Tick()
    {
        base.Tick();
        destination = destMap != null ? intendedTarget.Cell.ToVector3Shifted().ToBaseMapCoord(destMap) : intendedTarget.Cell.ToVector3Shifted();
    }

    protected override void ImpactSomething()
    {
        Impact(null);
    }

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (blockedByShield) return;

        var ziplineEnd = (ZiplineEnd)ThingMaker.MakeThing(VMF_DefOf.VMF_ZiplineEnd);
        ziplineEnd.launchVerb = launchVerb;
        ziplineEnd.rotation = ExactRotation.eulerAngles.y;
        launchVerb.ZiplineEnd = ziplineEnd;

        if (destMap != null)
        {
            if (destMap.IsVehicleMapOf(out var vehicle))
            {
                vehicle.PlayImpactSound(new VehicleComponent.DamageResult
                {
                    penetration = VehicleComponent.Penetration.Penetrated,
                    cell = intendedTarget.Cell.ToHitCell(vehicle)
                });
                ziplineEnd.rotation += vehicle.Angle;
            }
            else
            {
                SoundDefOf.BulletImpact_Ground.PlayOneShot(intendedTarget.ToTargetInfo(destMap));
            }
            GenSpawn.Spawn(ziplineEnd, intendedTarget.Cell, destMap);
        }
        else
        {
            SoundDefOf.BulletImpact_Ground.PlayOneShot(intendedTarget.ToTargetInfo(Map));
            GenSpawn.Spawn(ziplineEnd, intendedTarget.Cell, Map);
        }
        TargetMapManager.RemoveTargetInfo(launchVerb.caster);

        base.Destroy();

        if (launchVerb.CasterIsPawn)
        {
            launchVerb.OrderForceTarget(ziplineEnd);
        }
        else if (launchVerb.caster is Building_Turret building_Turret)
        {
            building_Turret.OrderAttack(ziplineEnd);
        }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        DrawZipline(drawLoc);
    }

    public void DrawZipline(Vector3 drawLoc)
    {
        if (launcher is { Spawned: true })
        {
            var num = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
            var drawPosA = drawLoc + (Vector3.forward * num) + (Vector3.back * ZiplineEnd.ZiplineEndOffset).RotatedBy(ExactRotation.eulerAngles.y);
            var caster = launchVerb.caster;
            var launcherPos = caster.DrawPos;
            var offset = caster.def.building?.turretTopOffset.ToVector3() ?? Vector3.zero;
            if (caster.IsOnNonFocusedVehicleMapOf(out var vehicle2))
            {
                offset = offset.RotatedBy(-vehicle2.Angle);
            }
            launcherPos += offset;
            var drawPosB = launcherPos + (Vector3.forward * ZiplineEnd.LauncherOffset).RotatedBy((drawPosA - launcherPos).AngleFlat());
            var y = Mathf.Max(drawPosA.y, drawPosB.y);
            GenDraw.DrawLineBetween(drawPosA.WithY(y), drawPosB.WithY(y), ZiplineEnd.ZiplineLayer, ZiplineEnd.ZiplineMat, ZiplineEnd.ZiplineWidth);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref launchVerb, "LaunchVerb");
        Scribe_References.Look(ref destMap, "destMap");
    }

    public Verb_LaunchZipline launchVerb;

    public Map destMap;
}
