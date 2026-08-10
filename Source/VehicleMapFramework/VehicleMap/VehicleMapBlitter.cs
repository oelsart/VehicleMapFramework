using System.Collections.Generic;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class VehicleMapBlitter(VehiclePawnWithMap vehicle) : IBlitTarget
{
  private static Material defaultMat;
  private static readonly int MainTex = Shader.PropertyToID("_MainTex");
  
  static VehicleMapBlitter()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      defaultMat = new Material(ShaderDatabase.Transparent);
    });
  }
  
  (int width, int height) IBlitTarget.TextureSize(in BlitRequest request)
  {
    const int SizePerCell = 64;
    if (vehicle.VehicleMap is null)
      return (0, 0);

    var size = vehicle.MapSize;
    var maxCells = Mathf.Max(size.x, size.z);
    return (maxCells * SizePerCell, maxCells * SizePerCell);
  }

  IEnumerable<RenderData> IBlitTarget.GetRenderData(Rect rect, BlitRequest request)
  {
    var textureSize = ((IBlitTarget)this).TextureSize(in request);
    var texture = VehicleMapUIRenderer.GetVehicleMapTexture(vehicle, request.rot.RotForVehicleDraw(),
      (textureSize.width, textureSize.height));
    defaultMat.mainTexture = texture;
    var renderRect = GetRenderRect(rect, request);
    yield return new RenderData(renderRect, texture, defaultMat, null, 0.1f, 0f);
  }

  public Rect GetRenderRect(Rect parentRect, BlitRequest request, bool fitToValidRect = false)
  {
    var vehicleRectSize = vehicle.VehicleDef.ScaleDrawRatio(parentRect.size);
    var vehicleMaxUi = Mathf.Max(vehicleRectSize.x, vehicleRectSize.y);
    var drawSizeMax = Mathf.Max(vehicle.VehicleDef.graphicData.drawSize.x, vehicle.VehicleDef.graphicData.drawSize.y);
    var pixelsPerCell = vehicleMaxUi / drawSizeMax;

    var mapSize = fitToValidRect ? vehicle.ValidMapRect.ExpandedBy(1).Size : vehicle.MapSize.ToIntVec2;
    float maxMapCells = Mathf.Max(mapSize.x, mapSize.z);
    var mapUiSize = new Vector2(maxMapCells * pixelsPerCell, maxMapCells * pixelsPerCell);

    var elongated = request.rot.IsHorizontal || request.rot.IsDiagonal;
    var vehicleGraphicData = vehicle.VehicleDef.graphicData;
    var vehicleDrawSize = new Vector2(vehicleGraphicData.drawSize.x, vehicleGraphicData.drawSize.y);
    if (elongated)
    {
      (vehicleDrawSize.x, vehicleDrawSize.y) = (vehicleDrawSize.y, vehicleDrawSize.x);
    }

    var scaleFactors = new Vector2(vehicleRectSize.x / vehicleDrawSize.x, vehicleRectSize.y / vehicleDrawSize.y);
    var drawOffset = vehicleGraphicData.DrawOffsetForRot(request.rot);
    var baseOffset = new Vector2(drawOffset.x * scaleFactors.x, -drawOffset.z * scaleFactors.y);

    var displayOffset = vehicle.VehicleDef.drawProperties.DisplayOffsetForRot(request.rot);
    var vehicleUiCenter = new Vector2(
      parentRect.center.x + (displayOffset.x * parentRect.width),
      parentRect.center.y + (displayOffset.y * parentRect.height)
    );

    var rawOffset = VehicleMapUtility.OffsetFor(vehicle, request.rot);
    if (fitToValidRect)
    {
      rawOffset +=
        (vehicle.ValidMapRect.CenterVector3 - vehicle.MapRect.CenterVector3)
        .RotatedBy(request.rot);
    }
    var mapUiCenter = new Vector2(
      vehicleUiCenter.x + (rawOffset.x * pixelsPerCell) + baseOffset.x,
      vehicleUiCenter.y + (-rawOffset.z * pixelsPerCell) + baseOffset.y // UIはY軸下向き
    );

    return new Rect(Vector2.zero, mapUiSize) { center = mapUiCenter };
  }
}