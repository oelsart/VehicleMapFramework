using System.Diagnostics.CodeAnalysis;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class Bullet_ZiplineEnd : Bullet, IZiplineEnd
{
    public CustomZipline.ZipLineData ZipLineData { get; set; }

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

    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
    {
        base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
        this.origin += (Vector3.forward * ZipLineData.LauncherOffset).RotatedBy(ExactRotation.eulerAngles.y);
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        destination = destMap != null ? intendedTarget.Cell.ToVector3Shifted().ToBaseMapCoord(destMap) : intendedTarget.Cell.ToVector3Shifted();
    }

    protected override void ImpactSomething()
    {
        Impact(null);
    }

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (blockedByShield) return;

        var ziplineEnd = (ZiplineEnd)ThingMaker.MakeThing(ZipLineData.ZiplineEndDef);
        ziplineEnd.launchVerb = launchVerb;
        ziplineEnd.rotation = ExactRotation.eulerAngles.y;
        ziplineEnd.ZipLineData = ZipLineData;
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

        base.Destroy();
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        DrawZipline(drawLoc);
    }

    public void DrawZipline(Vector3 drawLoc)
    {
        var num = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
        ZiplineEnd.DrawZipline(drawLoc + Vector3.forward * num, ExactRotation.eulerAngles.y, launchVerb, ZipLineData);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref launchVerb, "LaunchVerb");
        Scribe_References.Look(ref destMap, "destMap");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var customZipline = def.GetModExtension<CustomZipline>();
            if (customZipline != null)
            {
                ZipLineData = customZipline.zipLineData;
            }
        }
    }

    public Verb_LaunchZipline launchVerb;

    public Map destMap;
}
