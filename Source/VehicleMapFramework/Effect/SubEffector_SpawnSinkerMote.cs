using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class SubEffector_SpawnSinkerMote(SubEffecterDef subDef, Effecter parent) : SubEffecter(subDef, parent)
{
  private readonly SubEffecterDef subDef = subDef;

  private static readonly SimpleCurve ColorOverlayAlphaCurve =
  [
    new CurvePoint(0f, 0f),
    new CurvePoint(0.9f, 0.8f),
    new CurvePoint(0.98f, 0.8f),
    new CurvePoint(1f, 0f),
  ];
  
  public override void SubTrigger(TargetInfo A, TargetInfo B, int overrideSpawnTick = -1, bool force = false)
  {
    if (!A.Cell.TryGetFirstThing<VehiclePawn>(A.Map, out var vehicle)) return;
    
    if (vehicle.Map is null)
      return;
    const int PixelPerCell = 64;
    var rot = vehicle.FullRotation.RotForVehicleDraw();
    var mote = (MoteThrownSinker)ThingMaker.MakeThing(VMF_DefOf.VMF_MoteSink);
    var drawSize = vehicle.DrawSize;
    var drawSizeRotated = rot.IsHorizontal ? drawSize.Rotated() : drawSize;
    var texDrawSize = drawSizeRotated with
    {
      x = Mathf.NextPowerOfTwo(Mathf.CeilToInt(drawSizeRotated.x)),
      y = Mathf.NextPowerOfTwo(Mathf.CeilToInt(drawSizeRotated.y))
    };
    var textureSize = ((int)texDrawSize.x * PixelPerCell, (int)texDrawSize.y * PixelPerCell);
    
    mote.Texture = VehicleMapUIRenderer.CaptureVehicle(vehicle, rot, textureSize);
    mote.TexRotation = Quaternion.AngleAxis(-vehicle.ExtraAngle, Vector3.up);
    mote.TexDrawSize = texDrawSize.ToVector3().WithY(1f);
    mote.TexSize = textureSize;
    mote.Vehicle = vehicle;
    mote.Rot = rot;
    mote.OverlayColor = subDef.color;
    mote.ColorOverlayAlphaCurve = ColorOverlayAlphaCurve;
    mote.SetVelocity(subDef.angle.RandomInRange, subDef.speed.RandomInRange);
    mote.exactPosition = vehicle.DrawPos;
    GenSpawn.Spawn(mote, mote.exactPosition.ToIntVec3(), vehicle.Map);
  }
}