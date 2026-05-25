using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class MoteThrownSinker : MoteThrown
{

  private static readonly int MainTex = Shader.PropertyToID("_MainTex");
  private GraphicOverlay overlay;
  private Reactor_Sink reactor;
  private Vector3 texDrawSize;
  private Quaternion texRotation;
  private Vector2Int texSize;

  private Texture texture;
  private VehiclePawnWithMap vehicle;

  static MoteThrownSinker()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      TransparentMaterial = MaterialPool.MatFrom(ShaderDatabase.Transparent);
      SilhouetteMaterial = MaterialPool.MatFrom(ShaderDatabase.Silhouette);
      MaterialPropertyBlock = new MaterialPropertyBlock();
    });
  }

  public static Material TransparentMaterial { get; private set; }

  public static Material SilhouetteMaterial { get; private set; }

  public static MaterialPropertyBlock MaterialPropertyBlock { get; private set; }

  public virtual void SetParameters(Texture _texture, Quaternion _texRotation, Vector3 _texDrawSize, Vector2Int _texSize,
    VehiclePawnWithMap _vehicle, GraphicOverlay _overlay, Reactor_Sink _reactor)
  {
    texture = _texture;
    texRotation = _texRotation;
    texDrawSize = _texDrawSize;
    texSize = _texSize;
    vehicle = _vehicle;
    overlay = _overlay;
    reactor = _reactor;
  }

  protected override void TickInterval(int delta)
  {
    base.TickInterval(delta);
    MaintainTexture();
  }

  protected override void DrawAt(Vector3 drawLoc, bool flip = false)
  {
    if (texture is null) return;

    var properties = MaterialPropertyBlock;
    properties.Clear();
    properties.SetTexture(MainTex, texture);
    var pos = exactPosition.WithY(def.altitudeLayer.AltitudeFor() + yOffset);
    var matrix = Matrix4x4.TRS(pos, texRotation, texDrawSize);
    properties.SetColor(ShaderPropertyIDs.Color, Color.white.WithAlpha(Alpha));
    Graphics.DrawMesh(MeshPool.plane10, matrix, TransparentMaterial, 0, null, 0, MaterialPropertyBlock);

    properties.SetColor(ShaderPropertyIDs.Color, reactor.overlayColor.WithAlpha(reactor.colorOverlayAlphaCurve.Evaluate(AgeSecs / def.mote.Lifespan)));
    Graphics.DrawMesh(MeshPool.plane10, matrix, SilhouetteMaterial, 0, null, 0, MaterialPropertyBlock);
  }

  private void MaintainTexture()
  {
    if (vehicle is null || overlay is null) return;

    texture = VehicleMapUIRenderer.GetOverlayWithVehicleMapTexture(vehicle, overlay, Rot4.North, texSize, CellRect.Empty);
  }
}
