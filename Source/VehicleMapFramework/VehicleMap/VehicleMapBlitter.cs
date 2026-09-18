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
    var texW = Mathf.Max(1, size.x * SizePerCell);
    var texH = Mathf.Max(1, size.z * SizePerCell);
    return request.rot.IsHorizontal ? (texH, texW) : (texW, texH);
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

    var vehicleGraphicData = vehicle.VehicleDef.graphicData;
    var vehicleDrawSize = new Vector2(vehicleGraphicData.drawSize.x, vehicleGraphicData.drawSize.y);
    var horizontal = request.rot.IsHorizontal;
    var mapSize = fitToValidRect ? vehicle.ValidMapRect.ExpandedBy(1).Size : vehicle.MapSize.ToIntVec2;
    var scaleFactors = new Vector2(vehicleRectSize.x / vehicleDrawSize.x, vehicleRectSize.y / vehicleDrawSize.y);
    var mapUiSize = new Vector2(mapSize.x * scaleFactors.x, mapSize.z * scaleFactors.y);
    if (horizontal)
    {
      (mapUiSize.x, mapUiSize.y) = (mapUiSize.y, mapUiSize.x);
      (scaleFactors.x, scaleFactors.y) = (scaleFactors.y, scaleFactors.x);
    }

    var drawOffset = vehicleGraphicData.DrawOffsetForRot(request.rot);
    var baseOffset = new Vector2(drawOffset.x * scaleFactors.x, drawOffset.z * scaleFactors.y);

    var displayOffset = vehicle.VehicleDef.drawProperties.DisplayOffsetForRot(request.rot);
    var vehicleUiCenter = new Vector2(
      parentRect.center.x + (displayOffset.x * parentRect.width),
      parentRect.center.y + (displayOffset.y * parentRect.height)
    );

    var rawOffset = VehicleMapUtility.OffsetFor(vehicle, request.rot);
    if (fitToValidRect)
    {
      var mapRectOffset = vehicle.ValidMapRect.CenterVector3 - vehicle.MapRect.CenterVector3;
      rawOffset += mapRectOffset.RotatedBy(request.rot);
    }
    var mapUiCenter = new Vector2(
      vehicleUiCenter.x + (rawOffset.x * scaleFactors.x) + baseOffset.x,
      vehicleUiCenter.y + (-rawOffset.z * scaleFactors.y) - baseOffset.y // UIはY軸下向き
    );

    return new Rect(Vector2.zero, mapUiSize) { center = mapUiCenter };
  }
}