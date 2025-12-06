using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Bullet_ZiplineEndReturn : Bullet_ZiplineBase
{
    public override Quaternion ExactRotation => Quaternion.LookRotation((origin - destination).Yto0());

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
                if (launchVerb.caster.TryGetTargetMap(out var destMap) && destMap.IsVehicleMapOf(out var vehicle))
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

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (blockedByShield) return;
        Destroy();
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        base.Destroy(mode);
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

    public override void DrawZipline(Vector3 drawLoc)
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

    private LocalTargetInfo tmpTarget = LocalTargetInfo.Invalid;
}
