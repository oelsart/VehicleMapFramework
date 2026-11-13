using UnityEngine;
using Vehicles;
using Verse;
using SmashTools.Rendering;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class MoteThrownSinker : MoteThrown
{
  public static Material TransparentMaterial { get; private set; }

  public static Material SilhouetteMaterial { get; private set; }

  public static MaterialPropertyBlock MaterialPropertyBlock { get; private set; }

  static MoteThrownSinker()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      TransparentMaterial = MaterialPool.MatFrom(ShaderDatabase.Transparent);
      SilhouetteMaterial = MaterialPool.MatFrom(ShaderDatabase.Silhouette);
      MaterialPropertyBlock = new MaterialPropertyBlock();
    });
  }
  
  public Texture Texture { private get; set; }
  public Quaternion TexRotation { private get; set; }
  public Vector3 TexDrawSize { private get; set; }
  public (int, int) TexSize { private get; set; }
  public VehiclePawn Vehicle { private get; set; }
  public Rot4 Rot { private get; set; }
  public GraphicOverlay Overlay { private get; set; }
  public Color OverlayColor { private get; set; }
  public SimpleCurve ColorOverlayAlphaCurve { private get; set; }
  public bool DisposeOnDespawn { private get; set; }
  
  protected override void TickInterval(int delta)
  {
    base.TickInterval(delta);
    MaintainTexture();
  }

  public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
  {
    base.DeSpawn(mode);
    if (DisposeOnDespawn && Texture is RenderTexture renderTex)
    {
      renderTex.ReleaseAndDestroy();
    }
  }

  protected override void DrawAt(Vector3 drawLoc, bool flip = false)
  {
    if (Texture is null)
      return;

    var properties = MaterialPropertyBlock;
    properties.Clear();
    properties.SetTexture(AdditionalShaderPropertyIDs.MainTex, Texture);
    var pos = exactPosition.WithY(def.altitudeLayer.AltitudeFor() + yOffset);
    var matrix = Matrix4x4.TRS(pos, TexRotation, TexDrawSize);
    properties.SetColor(ShaderPropertyIDs.Color, Color.white.WithAlpha(Alpha));
    Graphics.DrawMesh(MeshPool.plane10, matrix, TransparentMaterial, 0, null, 0, MaterialPropertyBlock);

    properties.SetColor(ShaderPropertyIDs.Color,
      OverlayColor.WithAlpha(ColorOverlayAlphaCurve.Evaluate(AgeSecs / def.mote.Lifespan)));
    Graphics.DrawMesh(MeshPool.plane10, matrix, SilhouetteMaterial, 0, null, 0, MaterialPropertyBlock);
  }

  private void MaintainTexture()
  {
    if (Vehicle is null) return;

    Texture = Vehicle is VehiclePawnWithMap vehiclePawnWithMap && Overlay is not null
      ? VehicleMapUIRenderer.GetOverlayWithVehicleMapTexture(vehiclePawnWithMap, Overlay, Rot, TexSize, CellRect.Empty)
      : VehicleMapUIRenderer.CaptureVehicle(Vehicle, Rot, TexSize);
  }
}