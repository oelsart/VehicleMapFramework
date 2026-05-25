using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class Bullet_ZiplineEnd : Bullet_ZiplineBase
{
  public Map destMap;

  protected override Vector3 ExactDestination
  {
    get
    {
      var vector3 = intendedTarget.Cell.ToVector3Shifted();
      if (destMap is not null) vector3 = vector3.ToBaseMapCoord(destMap);
      if (this.IsOnNonFocusedVehicleMapOf(out var vehicle)) vector3 = vector3.ToVehicleMapCoord(vehicle);
      return vector3;
    }
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
          penetration = VehicleComponent.Penetration.Penetrated, cell = intendedTarget.Cell.ToHitCell(vehicle)
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
    Destroy();

    if (destMap is not null)
    {
      var ziplineEnd = (ZiplineEnd)ThingMaker.MakeThing(ZipLineData.ZiplineEndDef);
      ziplineEnd.launchVerb = launchVerb;
      ziplineEnd.rotation = ExactRotation.eulerAngles.y;
      ziplineEnd.ZipLineData = ZipLineData;
      GenSpawn.Spawn(ziplineEnd, intendedTarget.Cell, destMap);
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
    Scribe_References.Look(ref destMap, "destMap");
  }
}
