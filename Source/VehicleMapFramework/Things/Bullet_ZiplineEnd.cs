using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class Bullet_ZiplineEnd : Bullet_ZiplineBase
{
    public Map destMap;
    
    public int TicksToImpact => ticksToImpact;
    
    protected Vector3 ExactDestination => destMap != null
        ? intendedTarget.Cell.ToVector3Shifted().ToBaseMapCoord(destMap)
        : intendedTarget.Cell.ToVector3Shifted();
    
    public override void Launch(Thing _launcher, Vector3 _origin, LocalTargetInfo _usedTarget,
        LocalTargetInfo _intendedTarget, ProjectileHitFlags hitFlags, bool _preventFriendlyFire = false,
        Thing _equipment = null, ThingDef _targetCoverDef = null)
    {
        base.Launch(_launcher, _origin, _usedTarget, _intendedTarget, hitFlags, _preventFriendlyFire, _equipment, _targetCoverDef);
        destination = ExactDestination;
        origin += ExactRotation * (Vector3.forward * (ZipLineData.LauncherOffset + DrawSize.y / 2f));
        ticksToImpact = Mathf.CeilToInt(StartingTicksToImpact);
        if (ticksToImpact < 1)
        {
            ticksToImpact = 1;
        }
        lifetime = ticksToImpact;
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (intendedTarget.HasThing) destination = ExactDestination;
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        base.Destroy(mode);

        if (destMap != null)
        {
            if (destMap.IsVehicleMapOf(out var vehicle))
            {
                vehicle.PlayImpactSound(new VehicleComponent.DamageResult
                {
                    penetration = VehicleComponent.Penetration.Penetrated,
                    cell = intendedTarget.Cell.ToHitCell(vehicle)
                });
                return;
            }
            SoundDefOf.BulletImpact_Ground.PlayOneShot(intendedTarget.ToTargetInfo(destMap));
            return;
        }
        SoundDefOf.BulletImpact_Ground.PlayOneShot(intendedTarget.ToTargetInfo(Map));
    }

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (blockedByShield || hitThing != intendedTarget.Thing)
        {
            ZiplineEnd.ReturnZipline(launchVerb);
            return;
        }

        var map = Map;
        Destroy();
        
        var ziplineEnd = (ZiplineEnd)ThingMaker.MakeThing(ZipLineData.ZiplineEndDef);
        ziplineEnd.launchVerb = launchVerb;
        ziplineEnd.rotation = ExactRotation.eulerAngles.y;
        ziplineEnd.ZipLineData = ZipLineData;
        if (destMap.IsVehicleMapOf(out var vehicle))
            ziplineEnd.rotation += vehicle.Angle - vehicle.Transform.rotation;
        GenSpawn.Spawn(ziplineEnd, intendedTarget.Cell, vehicle?.VehicleMap ?? map);
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
        Scribe_References.Look(ref destMap, "destMap");
    }
}
