using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Bullet_ZiplineEndReturn : Bullet_ZiplineBase
{
    public override Quaternion ExactRotation => base.ExactRotation * Quaternion.AngleAxis(180f, Vector3.up);

    protected override Vector3 ExactDestination
    {
        get
        {
            if (launchVerb is { caster.Spawned: true })
            {
                var drawPos = launchVerb.caster.DrawPos;
                var offset = launcher.def.building?.turretTopOffset.ToVector3() ?? Vector3.zero;
                if (launcher.IsOnNonFocusedVehicleMapOf(out var vehicle) && !this.IsOnNonFocusedVehicleMap)
                {
                    offset = offset.RotatedBy(-vehicle.Angle + vehicle.Transform.rotation);
                }
                drawPos += offset;
                destination = drawPos + ExactRotation * (Vector3.forward * (ZipLineData.LauncherOffset + DrawSize.y / 2f));
            }
            return this.IsOnNonFocusedVehicleMapOf(out var vehicle2) ? destination.ToVehicleMapCoord(vehicle2) : destination;
        }
    }

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (blockedByShield) return;
        Destroy();
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
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var customZipline = launchVerb?.verbProps?.defaultProjectile?.GetModExtension<CustomZipline>();
            if (customZipline != null)
            {
                ZipLineData = customZipline.zipLineData;
            }
        }
    }
}
