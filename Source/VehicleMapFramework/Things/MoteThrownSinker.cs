using UnityEngine;
using Vehicles;
using Verse;
#if DEV
using SmashTools.Rendering;
#endif

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class MoteThrownSinker : MoteThrown
{
  public static Material TransparentMaterial { get; private set; }

  public static Material SilhouetteMaterial { get; private set; }

  public static MaterialPropertyBlock MaterialPropertyBlock { get; private set; }

  private static readonly int MainTex = Shader.PropertyToID("_MainTex");

  static MoteThrownSinker()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      TransparentMaterial = MaterialPool.MatFrom(ShaderDatabase.Transparent);
      SilhouetteMaterial = MaterialPool.MatFrom(ShaderDatabase.Silhouette);
      MaterialPropertyBlock = new MaterialPropertyBlock();
    });
  }

  private Texture texture;
  private Quaternion texRotation;
  private Vector3 texDrawSize;
  private Vector2Int texSize;
  private VehiclePawnWithMap vehicle;
  private GraphicOverlay overlay;
  private Color overlayColor;
  private SimpleCurve colorOverlayAlphaCurve;
  private bool disposeOnDespawn;

  public void SetParameters(RenderTexture _texture, Quaternion _texRotation, Vector3 _texDrawSize,
    Color _overlayColor, SimpleCurve _colorOverlayAlphaCurve)
  {
    texture = _texture;
    texRotation = _texRotation;
    texDrawSize = _texDrawSize;
    overlayColor = _overlayColor;
    colorOverlayAlphaCurve = _colorOverlayAlphaCurve;
    disposeOnDespawn = true;
  }
  
  public virtual void SetParameters(Texture _texture, Quaternion _texRotation, Vector3 _texDrawSize,
    Vector2Int _texSize,
    VehiclePawnWithMap _vehicle, GraphicOverlay _overlay, Color _overlayColor, SimpleCurve _colorOverlayAlphaCurve)
  {
    texture = _texture;
    texRotation = _texRotation;
    texDrawSize = _texDrawSize;
    texSize = _texSize;
    vehicle = _vehicle;
    overlay = _overlay;
    overlayColor = _overlayColor;
    colorOverlayAlphaCurve = _colorOverlayAlphaCurve;
  }

  protected override void TickInterval(int delta)
  {
    base.TickInterval(delta);
    MaintainTexture();
  }

  public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
  {
    base.DeSpawn(mode);
    if (disposeOnDespawn && texture is RenderTexture renderTex)
    {
#if DEV
      renderTex.ReleaseAndDestroy();
#else
      if (renderTex.IsCreated())
        renderTex.Release();
      Object.Destroy(renderTex);
#endif
    }
  }

  protected override void DrawAt(Vector3 drawLoc, bool flip = false)
  {
    if (texture is null)
      return;

    var properties = MaterialPropertyBlock;
    properties.Clear();
    properties.SetTexture(MainTex, texture);
    var pos = exactPosition.WithY(def.altitudeLayer.AltitudeFor() + yOffset);
    var matrix = Matrix4x4.TRS(pos, texRotation, texDrawSize);
    properties.SetColor(ShaderPropertyIDs.Color, Color.white.WithAlpha(Alpha));
    Graphics.DrawMesh(MeshPool.plane10, matrix, TransparentMaterial, 0, null, 0, MaterialPropertyBlock);

    properties.SetColor(ShaderPropertyIDs.Color,
      overlayColor.WithAlpha(colorOverlayAlphaCurve.Evaluate(AgeSecs / def.mote.Lifespan)));
    Graphics.DrawMesh(MeshPool.plane10, matrix, SilhouetteMaterial, 0, null, 0, MaterialPropertyBlock);
  }

  private void MaintainTexture()
  {
    if (vehicle is null || overlay is null) return;

    texture = VehicleMapUIRenderer.GetOverlayWithVehicleMapTexture(vehicle, overlay, Rot4.North, texSize,
      CellRect.Empty);
  }
}