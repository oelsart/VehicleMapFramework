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

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        
        launchVerb.caster.RemoveTargetInfo();
        if (launchVerb.CasterIsPawn)
            launchVerb.OrderForceTarget(this);
        else if (launchVerb.caster is Building_Turret building_Turret)
            building_Turret.OrderAttack(this);
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (launchVerb.caster is not { Spawned: true })
        {
            Destroy();
            return;
        }

        if ((launchVerb.caster is Pawn { TargetCurrentlyAimingAt: var target } && target != this) ||
            (launchVerb.caster is Building_Turret { ForcedTarget: var target2 } && target2 != this) ||
            !launchVerb.TryFindShootLineFromToOnVehicle(launchVerb.caster.PositionOnBaseMap, this.PositionOnBaseMap, out _))
        {
            Destroy();
        }
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (launchVerb.caster is { Spawned: true })
        {
            var pos = launchVerb.caster.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned
                ? Position : this.PositionOnBaseMap;
            var bullet = (Bullet_ZiplineEndReturn)GenSpawn.Spawn(ZipLineData.ZiplineReturnDef, pos, this.BaseMap());
            bullet.launchVerb = launchVerb;
            bullet.ZipLineData = ZipLineData;
            launchVerb.ZiplineEnd = bullet;
            bullet.Launch(launchVerb.caster, this.TrueCenter(), launchVerb.caster, launchVerb.caster, ProjectileHitFlags.IntendedTarget);
        }
        else launchVerb.ZiplineEnd = null;
        base.Destroy(mode);
    }

    public override void Print(SectionLayer layer)
    {
        Graphic.Print(layer, this, rotation);
        foreach (var comp in AllComps)
        {
            comp.PostPrintOnto(layer);
        }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        if (def.drawerType == DrawerType.RealtimeOnly && launchVerb is { caster.Spawned: true })
        {
            rotation = (drawLoc - launchVerb.caster.DrawPos).AngleFlat();
            Graphic.Draw(drawLoc, Rot4.North, this, rotation);
        }
        Comps_DrawAt(drawLoc, flip);
        Comps_PostDraw();

        SilhouetteUtility.DrawGraphicSilhouette(this, drawLoc);
        DrawZipline(drawLoc);
    }

    public void DrawZipline(Vector3 drawLoc)
    {
        var rot = rotation;
        DrawZipline(drawLoc, rot, launchVerb, ZipLineData);
    }

    public static void DrawZipline(Vector3 drawLoc, float rotation, Verb_LaunchZipline launchVerb, CustomZipline.ZipLineData ziplineData)
    {
        var launcher = launchVerb.caster;
        if (launcher is null || !launcher.Spawned)
            return;
        var drawPosA = drawLoc + (Vector3.back * ziplineData.ZiplineEndOffset).RotatedBy(rotation);
        var launcherPos = launcher.DrawPos;
        var offset = launcher.def.building?.turretTopOffset.ToVector3() ?? Vector3.zero;
        if (launcher.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            offset = offset.RotatedBy(-vehicle.Angle + vehicle.Transform.rotation);
        }

        launcherPos += offset;
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
