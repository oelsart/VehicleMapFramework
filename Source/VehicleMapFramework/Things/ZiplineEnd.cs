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
        launchVerb?.ziplineEnd = this;
        if (launchVerb is { caster: Building_TurretGunForcedTargetOnly turret })
        {
            turret.RemoveTargetInfo();
            turret.ForcedTarget = this;
        }
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (launchVerb is not { caster.SpawnedOrAnyParentSpawned: true } || launchVerb.ziplineEnd != this)
            base.Destroy();
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        ReturnZipline(launchVerb);
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
        var launcher = launchVerb?.caster;
        if (launcher is not { Spawned: true })
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
    
    public static void ReturnZipline(Verb_LaunchZipline launchVerb)
    {
        if (launchVerb is { caster.Spawned: true, ziplineEnd: IZiplineEnd ziplineEnd })
        {
            var thing = launchVerb.ziplineEnd;
            var pos = launchVerb.caster.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned
                ? thing.Position : thing.PositionOnBaseMap;
            var bullet = (Bullet_ZiplineEndReturn)ThingMaker.MakeThing(ziplineEnd.ZipLineData.ZiplineReturnDef);
            bullet.launchVerb = launchVerb;
            bullet.ZipLineData = ziplineEnd.ZipLineData;
            GenSpawn.Spawn(bullet, pos, thing.GroundMap);
            var spawnedParentOrMe = launchVerb.caster.SpawnedParentOrMe;
            bullet.Launch(launchVerb.caster, thing.DrawPos, spawnedParentOrMe, spawnedParentOrMe, ProjectileHitFlags.IntendedTarget);
        }
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
