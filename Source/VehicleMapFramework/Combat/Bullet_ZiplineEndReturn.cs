using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Bullet_ZiplineEndReturn : Bullet, IZiplineEnd
{
    public CustomZipline.ZipLineData ZipLineData { get; set; }

    public override Quaternion ExactRotation => Quaternion.LookRotation((origin - destination).Yto0());

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

    protected override void TickInterval(int delta)
    {
        if (launchVerb.caster?.Spawned ?? false)
        {
            destination = launchVerb.caster.DrawPos +
                          (Vector3.forward * (ZipLineData.LauncherOffset + 0.5f - ZipLineData.ZiplineEndOffset))
                          .RotatedBy(ExactRotation.eulerAngles.y);

            //戻ってる間砲塔をこっちに向けとくためにOrderAttackしとく
            if (launchVerb.caster is Building_Turret building_Turret)
            {
                var forcedTarget = building_Turret.ForcedTarget;
                var originCell = origin.ToIntVec3();
                if (TargetMapManager.HasTargetMap(launchVerb.caster, out var destMap) && destMap.IsVehicleMapOf(out var vehicle))
                {
                    originCell = originCell.ToVehicleMapCoord(vehicle);
                }
                if (forcedTarget != originCell)
                {
                    tmpTarget = forcedTarget;
                    building_Turret.OrderAttack(originCell);
                }
            }
        }
        base.TickInterval(delta);
    }
    protected override void ImpactSomething()
    {
        Impact(null);
    }

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (blockedByShield) return;

        Destroy();
        launchVerb.ZiplineEnd = null;
        if (launchVerb.caster is Building_Turret building_Turret)
        {
            building_Turret.OrderAttack(tmpTarget);
        }
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
        Scribe_References.Look(ref launchVerb, "launchVerb");
        Scribe_TargetInfo.Look(ref tmpTarget, "tmpTarget");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var customZipline = launchVerb?.verbProps?.defaultProjectile?.GetModExtension<CustomZipline>();
            if (customZipline != null)
            {
                ZipLineData = customZipline.zipLineData;
            }
        }
    }

    public Verb_LaunchZipline launchVerb;

    private LocalTargetInfo tmpTarget = LocalTargetInfo.Invalid;
}
