using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
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
    const int PixelPerCell = 128;
    var request = BlitRequest.For(vehicle);
    request.rot = vehicle.FullRotation;
    var drawSize = vehicle.DrawSize / vehicle.VehicleDef.uiIconScale;
    var max = Mathf.Max(drawSize.x, drawSize.y);
    var drawSizeMax = new Vector2(max, max);
    var rect = new Rect(Vector2.zero, drawSizeMax * PixelPerCell);
    var renderTex = VehicleGui.CreateRenderTexture(rect, in request);
    VehicleGui.Blit(renderTex, rect, in request);
      
    var mote = (MoteThrownSinker)ThingMaker.MakeThing(VMF_DefOf.VMF_MoteSink);
    mote.SetParameters(
      renderTex,
      Quaternion.AngleAxis(-vehicle.ExtraAngle, Vector3.up),
      drawSizeMax.ToVector3().SetToAltitude(AltitudeLayer.MoteLow),
      subDef.color,
      ColorOverlayAlphaCurve);
    mote.SetVelocity(subDef.angle.RandomInRange, subDef.speed.RandomInRange);
    mote.exactPosition = vehicle.DrawPos;
    GenSpawn.Spawn(mote, mote.exactPosition.ToIntVec3(), vehicle.Map);
  }
}