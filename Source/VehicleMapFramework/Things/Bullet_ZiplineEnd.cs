using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class Bullet_ZiplineEnd : Bullet_ZiplineBase
{
    public override void Launch(Thing launcher_, Vector3 origin_, LocalTargetInfo usedTarget_,
        LocalTargetInfo intendedTarget_, ProjectileHitFlags hitFlags, bool preventFriendlyFire_ = false,
        Thing equipment_ = null, ThingDef targetCoverDef_ = null)
    {
        base.Launch(launcher_, origin_, usedTarget_, intendedTarget_, hitFlags, preventFriendlyFire_, equipment_, targetCoverDef_);
        this.origin += (Vector3.forward * ZipLineData.LauncherOffset).RotatedBy(ExactRotation.eulerAngles.y);
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        destination = destMap != null ? intendedTarget.Cell.ToVector3Shifted().ToBaseMapCoord(destMap) : intendedTarget.Cell.ToVector3Shifted();
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
                ziplineEnd.rotation += vehicle.Angle - vehicle.Transform.rotation;
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

    public override void DrawZipline(Vector3 drawLoc)
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

    public Map destMap;
}
