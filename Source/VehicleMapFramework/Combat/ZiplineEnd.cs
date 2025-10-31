using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class ZiplineEnd : ThingWithComps, IZiplineEnd
{
    public Verb_LaunchZipline launchVerb;

    public float rotation;
    
    public CustomZipline.ZipLineData ZipLineData { get; set; }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (!launchVerb.caster?.Spawned ?? false)
        {
            Destroy();
            return;
        }
        if ((launchVerb.caster is Pawn pawn && pawn.TargetCurrentlyAimingAt != this) ||
            (launchVerb.caster is Building_Turret building_Turret && building_Turret.ForcedTarget != this) ||
            launchVerb.OutOfRange(launchVerb.caster.PositionOnBaseMap(), this, this.MovedOccupiedRect()) ||
            !GenSightOnVehicle.LineOfSightThingToThing(launchVerb.caster, this))
        {
            Destroy();
        }
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (launchVerb.caster?.Spawned ?? false)
        {
            var bullet = (Bullet_ZiplineEndReturn)GenSpawn.Spawn(ZipLineData.ZiplineReturnDef, this.PositionOnBaseMap(), this.BaseMap());
            bullet.launchVerb = launchVerb;
            bullet.ZipLineData = ZipLineData;
            launchVerb.ZiplineEnd = bullet;
            bullet.Launch(launchVerb.caster, this.TrueCenter(), launchVerb.caster, launchVerb.caster, ProjectileHitFlags.IntendedTarget);
        }
        base.Destroy(mode);
    }

    public override void Print(SectionLayer layer)
    {
        Graphic.Print(layer, this, VehicleMapUtility.PrintExtraRotation(this) + rotation);
        foreach (var comp in AllComps)
        {
            comp.PostPrintOnto(layer);
        }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        DrawZipline(drawLoc);
    }

    public void DrawZipline(Vector3 drawLoc)
    {
        var rot = rotation;
        if (this.IsOnVehicleMapOf(out var vehicle))
        {
            rot -= vehicle.Angle;
        }

        DrawZipline(drawLoc, rot, launchVerb, ZipLineData);
    }

    public static void DrawZipline(Vector3 drawLoc, float rotation, Verb_LaunchZipline launchVerb, CustomZipline.ZipLineData ziplineData)
    {
        var launcher = launchVerb.caster;
        if (launcher is null || !launcher.Spawned)
            return;
        var drawPosA = drawLoc + (Vector3.back * ziplineData.ZiplineEndOffset).RotatedBy(rotation);
        var launcherPos = launcher.DrawPos;
        var drawPosB = launcherPos + (Vector3.forward * ziplineData.LauncherOffset).RotatedBy((drawPosA - launcherPos).AngleFlat());
        var y = Mathf.Max(drawPosA.y, drawPosB.y) + Altitudes.AltInc;
        GenDrawOnVehicle.DrawLineBetweenInstanced(drawPosA.WithY(y), drawPosB.WithY(y), ziplineData.ZiplineMat, ziplineData.ZiplineWidth);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref launchVerb, "launchVerb");
        Scribe_Values.Look(ref rotation, "rotation");
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
