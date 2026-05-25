using RimWorld;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public abstract class Bullet_ZiplineBase : Bullet, IZiplineEnd
{
  public Verb_LaunchZipline launchVerb;

  public override int UpdateRateTicks
  {
    get
    {
      var baseRate = base.UpdateRateTicks;
      if (baseRate == 1) return baseRate;
      return Find.CurrentMap.BaseMapOrCaravan == this.BaseMapOrCaravan ? 1 : baseRate;
    }
  }

  protected Quaternion ExactRotationOrigin
  {
    get
    {
      var rotation = ExactRotation;
      if (this.IsOnNonFocusedVehicleMapOf(out var vehicle))
      {
        rotation *= Quaternion.AngleAxis(-vehicle.FullAngle, Vector3.up);
      }
      return rotation;
    }
  }

  protected abstract Vector3 ExactDestination { get; }

  protected float ArcHeightFactor
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

  public CustomZipline.ZipLineData ZipLineData { get; set; }

  public abstract void DrawZipline(Vector3 drawLoc);

  public override void Launch(Thing _launcher, Vector3 _origin, LocalTargetInfo _usedTarget,
    LocalTargetInfo _intendedTarget, ProjectileHitFlags hitFlags, bool _preventFriendlyFire = false,
    Thing _equipment = null, ThingDef _targetCoverDef = null)
  {
    if (this.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      _origin = _origin.ToVehicleMapCoord(vehicle);
    }
    base.Launch(_launcher, _origin, _usedTarget, _intendedTarget, hitFlags, _preventFriendlyFire, _equipment, _targetCoverDef);
    destination = ExactDestination;
    if (this is Bullet_ZiplineEnd)
      origin += ExactRotationOrigin * (Vector3.forward * (ZipLineData.LauncherOffset + DrawSize.y / 2f));
    ticksToImpact = Mathf.CeilToInt(StartingTicksToImpact);
    if (ticksToImpact < 1)
    {
      ticksToImpact = 1;
    }
    lifetime = ticksToImpact;
  }

  public override void SpawnSetup(Map map, bool respawningAfterLoad)
  {
    base.SpawnSetup(map, respawningAfterLoad);
    launchVerb?.ziplineEnd = this;
  }

  protected override void TickInterval(int delta)
  {
    destination = ExactDestination;
    if (!this.IsOnNonFocusedVehicleMap || landed)
    {
      base.TickInterval(delta);
      return;
    }

    var rect = new Rect(Vector2.zero, Patch_Map_MapUpdate.MeshSize);
    if (!rect.Contains(DrawPos.ToVector2()))
    {
      Destroy();
      return;
    }

    lifetime -= delta;
    ticksToImpact -= delta;
    var newPos = ExactPosition.ToIntVec3();
    if (newPos.InBounds(Map))
    {
      Position = newPos;
    }
    if (ticksToImpact <= 0)
    {
      ImpactSomething();
    }
  }

  protected override void ImpactSomething()
  {
    Impact(intendedTarget.Thing);
  }

  public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
  {
    base.Destroy(mode);
    launchVerb?.ziplineEnd = null;
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_References.Look(ref launchVerb, "LaunchVerb");
    if (Scribe.mode == LoadSaveMode.PostLoadInit)
    {
      var customZipline = def.GetModExtension<CustomZipline>();
      if (customZipline != null)
      {
        ZipLineData = customZipline.zipLineData;
      }
    }
  }
}
